using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Helios.Classes.Tokens;
using Helios.Configuration;
using Helios.Database.Tables.Account;
using Helios.Utilities.Extensions;
using Jose;
using Newtonsoft.Json;

namespace Helios.Utilities.Tokens
{
    public static class TokenUtilities
    {
        private static async Task<string> CreateTokenAsync(string clientId, string grantType, User user, string type)
        {
            var isAccess = type == "access";
            var expiry = isAccess ? TokenSettings.AccessTokenExpiry : TokenSettings.RefreshTokenExpiry;
            
            var payload = new
            {
                App = "fortnite",
                Sub = user.AccountId,
                Dvid = new Random().Next(1, 1000000000),
                Mver = false,
                Clid = clientId,
                Dn = user.Username,
                Am = type == "access" ? grantType : "refresh",
                P = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                Iai = user.AccountId,
                Sec = 1,
                Clsvc = "fortnite",
                T = "s",
                Ic = true,
                Jti = Guid.NewGuid().ToString(),
                CreationDate = DateTime.UtcNow.ToIsoUtcString(),
                ExpiresIn = expiry
            };

            var token = JWT.Encode(payload, Encoding.UTF8.GetBytes(Constants.config.JWTClientSecret), JwsAlgorithm.HS256);

            await Constants.repositoryPool
                .GetRepository<Database.Tables.Account.Tokens>()
                .SaveAsync(new Database.Tables.Account.Tokens
                {
                    Type = $"{type}token",
                    AccountId = user.AccountId,
                    Token = token
                });

            return token;
        }

        public static Task<string> CreateAccessTokenAsync(string clientId, string grantType, User user) =>
            CreateTokenAsync(clientId, grantType, user, "access");

        public static Task<string> CreateRefreshTokenAsync(string clientId, User user) =>
            CreateTokenAsync(clientId, "", user, "refresh");
    }
}
