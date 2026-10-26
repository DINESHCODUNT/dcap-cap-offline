using DigitalCap.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalCap.Core.DTO
{
    public class EditProfileRequest
    {
        //public ApplicationUser AspNetUser { get; set; }
        public UserAccountModel UserAccount { get; set; }
    }

}
