using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ranalo.Knox.ConsoleApp
{
    internal class AccessTokenResponse
    {
        public string AccessToken { get; set; }
        public int ValidityInMinutes { get; set; }
    }
}
