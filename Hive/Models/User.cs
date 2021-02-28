﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Hive.Models
{
    /// <summary>
    /// A moderately thin proxy to the auth server, holds some information that will probably be useful.
    /// </summary>
    public class User
    {
        // TODO: this should be from the authentication client library

        // this should have at least:
        // - name
        // - unique id (might be name?)
        // - references to localization objects ala LocalizedModInfo with:
        //   - bio
        // - links to various info about the user
        // - a prfile pic
        // - an extra data object like Mod.AdditionalData

        /// <summary>
        /// The username of the user.
        /// </summary>
        [Key]
        public string Username { get; set; } = null!;

        /// <summary>
        /// The additional data attached to the user object.
        /// </summary>
        public Dictionary<string, JsonElement> AdditionalData { get; set; } = new Dictionary<string, JsonElement>();
    }
}
