﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityServer4;
using IdentityServer4.Extensions;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using Jp.Database.Context;
using Jp.Database.Identity;
using Jp.UI.SSO.Models;
using Jp.UI.SSO.Util;
using JPProject.Domain.Core.Util;
using JPProject.Sso.AspNetIdentity.Models.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MultiTenancyServer;

namespace Jp.UI.SSO.Controllers.Tenants
{
    public class TenantsController : Controller
    {
        private readonly SsoContext _context;
        private readonly IIdentityServerInteractionService _interaction;
        private readonly IClientStore _clientStore;
        private readonly TenantManager<Tenant> _tenantManager;
        private readonly IdentityUserManager _userManager;

        public TenantsController(SsoContext context, IIdentityServerInteractionService interaction,
            IClientStore clientStore, TenantManager<Tenant> tenantManager, IdentityUserManager userManager)
        {
            _context = context;
            _interaction = interaction;
            _clientStore = clientStore;
            _tenantManager = tenantManager;
            _userManager = userManager;
        }
        public async Task<IActionResult> Index(string returnUrl = "")
        {
            var vm  = new TenantViewModel();
            var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
            if (context?.ClientId != null)
            {
                var client = await _clientStore.FindEnabledClientByIdAsync(context.ClientId);
                if (client != null)
                {
                    vm.ClientLogo = client.LogoUri;
                }
            }
            var sub = User.GetSubjectId();
            var tenants = await (from tenant in _context.Tenants
                join userRole in _context.UserRoles on tenant.Id equals userRole.TenantId
                where userRole.UserId == sub
                select tenant).Distinct().ToListAsync();
            var count = tenants.Count;
            if (count < 1) return NotFound("No Tenant Found");
            //if (tenants.Count == 1)
            //{
            //    var tenant = tenants.FirstOrDefault();
            //    return await Select(Guid.Parse(tenant!.Id),  tenant.CanonicalName, returnUrl);
            //}
            ViewBag.referer = Request.Headers["Referer"].ToString();
            vm.Tenants = tenants;
            vm.ReturnUrl = returnUrl;
            return View(vm);
        }
        [HttpPost]
        public async Task<IActionResult> Select(Guid id, string name, string returnUrl)
        {
            var claims = new List<Claim>(HttpContext.User.Claims)
            {
                new Claim("tid", id.ToString()), new Claim("tname", name)
            };
            await HttpContext.SignInAsync(User.Claims.Single(r => r.Type == "sub").Value, claims.ToArray());
            return Redirect(returnUrl);
        }

        [HttpGet]
        public async Task<IActionResult> Onboarding(string returnUrl = "")
        {
            if (await CheckBusinessAlreadyCreatedByUser(User.GetSubjectId()))
            {
                return BadRequest("Onboarding Already Completed");
            }
            return View(new RegisterTenantViewModel{ReturnUrl = returnUrl});
        }

        [HttpPost]
        public async Task<IActionResult> Onboarding(RegisterTenantViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                // something went wrong, show form with error
                return View();
            }

           
            if (await _tenantManager.Tenants.AnyAsync(x =>
                x.CanonicalName.Trim().ToLower().Equals(vm.Name.Trim().ToLower())))
            {
                ModelState.AddModelError("Conflict","Business name already exist");
                return View();
            }

            var newTenant = new Tenant(vm.Name,vm.Name);
            await _tenantManager.CreateAsync(newTenant);

            var userId = User.GetSubjectId();
            var user =  await _userManager.FindByIdAsync(userId);
            await _userManager.AddToRolesAsync(user, newTenant, new[] {Roles.Owner});
            user.ProfileCompleted = true;
            await _userManager.UpdateAsync(user);
            var claims = User.Claims;
            claims = claims.Where(x => x.Type != ClaimExtensions.ProfileInComplete);
            claims = claims.Concat(new[]
            {
                new Claim("tid", newTenant.Id.ToString()), new Claim("tname", newTenant.CanonicalName)
            });
            await HttpContext.SignInAsync(User.GetSubjectId(), claims.ToArray());
            return IEnumerableExtensions.IsNullOrEmpty(vm.ReturnUrl) ?
                Redirect("~/Grants") :
                Redirect(vm.ReturnUrl);
        }

        private Task<bool> CheckBusinessAlreadyCreatedByUser(string userId)
            => (from userRole in _context.UserRoles
                join tenant in _context.Tenants on userRole.TenantId equals tenant.Id
                where userRole.UserId == userId
                select tenant).AnyAsync();

    }
}
