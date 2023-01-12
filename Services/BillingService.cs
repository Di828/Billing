using Grpc.Core;

namespace Billing.Services
{
    public class BillingService : Billing.BillingBase
    {
        private readonly ILogger<BillingService> _logger;
        private static List<User> _users = new List<User> 
        { 
            new User() {Name = "boris", Raiting = 5000 },
            new User() {Name = "maria", Raiting = 1000 },
            new User() {Name = "oleg", Raiting = 800 },
        };
        private long _totalCoins;

        public BillingService(ILogger<BillingService> logger)
        {
            _logger = logger;
        }

        public override async Task<Response> CoinsEmission(EmissionAmount request, ServerCallContext context)
        {
            if (request.Amount < 0)
                return new Response() 
                { 
                    Status = Response.Types.Status.Failed, 
                    Comment = "Emission can't be less than 0" 
                };
            if (!IsDistributed(request.Amount))
                return new Response()
                {
                    Status = Response.Types.Status.Failed,
                    Comment = "It is impossible to distribute coins with this implementation"
                };
            return new Response()
            {
                Status = Response.Types.Status.Ok,
                Comment = "Sucsess"
            };
        }
        public override async Task ListUsers(None request, IServerStreamWriter<UserProfile> responseStream, ServerCallContext context)
        {          
            foreach (var user in _users)
            {
                await responseStream.WriteAsync(new UserProfile() {Name = user.Name,Amount=user.Coins.Count });
            }
        }
        public override async Task<Response> MoveCoins(MoveCoinsTransaction request, ServerCallContext context)
        {
            User sender;
            User receiver;
            try
            {
                sender = _users.Find(x => x.Name == request.SrcUser)!;
                receiver = _users.Find(x => x.Name == request.DstUser)!;
            }
            catch
            {
                return new Response()
                {
                    Status = Response.Types.Status.Failed,
                    Comment = "User Not Found"
                };
            }
            if (sender.Coins.Count < request.Amount)
            {
                return new Response()
                {
                    Status = Response.Types.Status.Failed,
                    Comment = "User have not enough coins to send"
                };
            }
            for (int i = 0; i < request.Amount; i++)
            {
                var sendedCoin = sender.Coins[0];
                sender.Coins.RemoveAt(0);
                sendedCoin.History += $";{receiver.Name}";
                receiver.Coins.Add(sendedCoin);
            }
            return new Response()
            {
                Status = Response.Types.Status.Ok,
                Comment = "Sucsess"
            };
        }
        public override async Task<Coin> LongestHistoryCoin(None request, ServerCallContext context)
        {
            var longestHistoryCoin = _users[0].Coins[0];
            foreach (var user in _users)
                foreach (var coin in user.Coins)
                    if (CoinHistoryLength(coin) > CoinHistoryLength(longestHistoryCoin))
                        longestHistoryCoin = coin;
            return longestHistoryCoin;
        }
        private int CoinHistoryLength(Coin coin)
        {
            return coin.History.Split(';').Length;
        }
        private bool IsDistributed(long amount)
        {
            var raitingSum = _users.Sum(x => x.Raiting);
            var coinsToDistribute = amount;

            // Distribute logic
            foreach (var user in _users)
            {
                user.FullCoinsOnDistribution = (user.Raiting * amount) / raitingSum;
                user.CoinPartOnDistribution = (user.Raiting * amount) % raitingSum;
                coinsToDistribute -= user.FullCoinsOnDistribution;
            }
            while (coinsToDistribute > 0)
            {
                var userForNextCoin = _users.First(x => x.CoinPartOnDistribution == _users.Max(x => x.CoinPartOnDistribution));
                userForNextCoin.FullCoinsOnDistribution++;
                userForNextCoin.CoinPartOnDistribution = 0;
                coinsToDistribute--;
            }
            
            foreach (var user in _users)
                if (user.FullCoinsOnDistribution == 0)
                    return false;
            
            foreach (var user in _users)
            {
                for (long i = _totalCoins + 1; i < _totalCoins + 1 + user.FullCoinsOnDistribution; i++)
                    user.Coins.Add(new Coin() { Id = i, History = $"{user.Name}" });                
                _totalCoins += user.FullCoinsOnDistribution;                
            }
            return true;
            
        }
    }
    public class User
    {
        public string Name { get; set; }
        public int Raiting { get; set; }        
        public List<Coin> Coins { get; set; } = new List<Coin>();
        public long FullCoinsOnDistribution { get; set; }
        public long CoinPartOnDistribution { get; set; }
    }
}
