﻿// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


namespace Jp.UI.SSO.Models
{
    public class LogoutViewModel : LogoutInputModel
    {
        public LogoutViewModel() { }
        public bool ShowLogoutPrompt { get; set; }
        public string PostLogoutRedirectUri { get; set; }
        public string Client { get; set; }
        public string ClientLogo { get; set; }
    }



}
