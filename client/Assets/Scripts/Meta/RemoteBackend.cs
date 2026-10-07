#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SanGuo.Core;
using SanGuo.Core.Data;
using SanGuo.Core.Meta;
using UnityEngine;
using UnityEngine.Networking;

namespace SanGuo.Client
{
    /// <summary>
    /// 伺服器後端：呼叫 SanGuo.Server 的 HTTP 端點。規則與結算都在伺服器，
    /// 客戶端只交出操作紀錄。帳號辨識目前是 X-Account 標頭（占位，正式版換 token）。
    /// </summary>
    public sealed class RemoteBackend : IGameBackend
    {
        private readonly string _baseUrl;
        private readonly string _account;
        private bool _loggedIn;

        public string Name => "伺服器";

        public RemoteBackend(string baseUrl, string account)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _account = account;
        }

        private sealed class Response
        {
            public bool Ok;
            public string Code = "network";
            public Dictionary<string, object?> Data = new Dictionary<string, object?>();
        }

        private async Task<Response> Send(string method, string path, object? body = null)
        {
            var request = new UnityWebRequest(_baseUrl + path, method) { downloadHandler = new DownloadHandlerBuffer() };
            request.timeout = 10;
            request.SetRequestHeader("X-Account", _account);
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(MiniJson.Write(body)));
                request.SetRequestHeader("Content-Type", "application/json");
            }

            var tcs = new TaskCompletionSource<bool>();
            request.SendWebRequest().completed += _ => tcs.TrySetResult(true);
            await tcs.Task;

            try
            {
                string text = request.downloadHandler.text;
                // 伺服器業務錯誤是 400 + JSON；連不上或其他錯誤沒有 JSON。
                if (text.Length > 0 && MiniJson.Parse(text) is Dictionary<string, object?> root && root.ContainsKey("ok"))
                {
                    var r = new Response { Ok = root["ok"] is true, Code = root["code"] as string ?? "error" };
                    if (root.TryGetValue("data", out var d) && d is Dictionary<string, object?> dd) r.Data = dd;
                    return r;
                }
                Debug.LogWarning($"[net] {method} {path} -> {request.result} {request.error}");
                return new Response();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[net] {method} {path} 回應解析失敗：{e.Message}");
                return new Response();
            }
            finally
            {
                request.Dispose();
            }
        }

        private static int Int(Dictionary<string, object?> d, string key) =>
            d.TryGetValue(key, out var v) && v is long l ? (int)l : v is double db ? (int)db : 0;

        public async Task<ProfileView?> GetProfile()
        {
            // 第一次先登入（建立帳號 / 換日），之後只讀。
            var r = _loggedIn ? await Send("GET", "/profile") : await Send("POST", "/login");
            if (!r.Ok) return null;
            _loggedIn = true;

            // 伺服器回的是完整存檔（同 ProfileSerializer 格式）加上換算好的體力。
            var d = r.Data;
            var view = ProfileView.From(ProfileSerializer.FromObject(d), 0);
            if (d.TryGetValue("stamina", out var st) && st is Dictionary<string, object?> sd)
            {
                view.Stamina = Int(sd, "current");
                view.StaminaCap = Int(sd, "cap");
            }
            return view;
        }

        public async Task<StartStageResult> StartStage(string stageId, IReadOnlyList<FormationEntry>? formation = null)
        {
            var body = new Dictionary<string, object?> { ["stageId"] = stageId };
            if (formation != null)
            {
                var list = new List<object?>();
                foreach (var f in formation)
                    list.Add(new Dictionary<string, object?> { ["heroId"] = f.HeroId, ["lane"] = (long)f.Lane, ["row"] = (long)f.Row });
                body["formation"] = list;
            }
            var r = await Send("POST", "/stage/start", body);
            var result = new StartStageResult { Ok = r.Ok, Code = r.Code };
            if (r.Ok && r.Data.TryGetValue("seed", out var s) && s is long seed) result.Seed = (ulong)seed;
            else if (r.Ok) { result.Ok = false; result.Code = "network"; }
            return result;
        }

        public async Task<FinishStageResult> FinishStage(string stageId, IReadOnlyList<ReplayAction> actions)
        {
            var list = new List<object?>();
            foreach (var a in actions)
            {
                string kind = a.Kind == ReplayActionKind.Play ? "play" : a.Kind == ReplayActionKind.Move ? "move" : "end";
                list.Add(new Dictionary<string, object?>
                {
                    ["kind"] = kind, ["cardId"] = (long)a.CardId, ["unitId"] = (long)a.UnitId,
                    ["lane"] = (long)a.Lane, ["row"] = (long)a.Row, ["targetId"] = (long)a.TargetId,
                });
            }
            var r = await Send("POST", "/stage/finish", new Dictionary<string, object?> { ["stageId"] = stageId, ["actions"] = list });
            var d = r.Data;
            return new FinishStageResult
            {
                Ok = r.Ok, Code = r.Code,
                Won = d.TryGetValue("won", out var w) && w is true,
                Stars = Int(d, "stars"),
                FirstClear = d.TryGetValue("firstClear", out var f) && f is true,
                Exp = Int(d, "exp"), Gold = Int(d, "gold"), Yuanbao = Int(d, "yuanbao"),
                LevelsGained = Int(d, "levelsGained"),
                Materials = d.TryGetValue("materials", out var m) && m is Dictionary<string, object?> md
                    ? md.ToDictionary(kv => kv.Key, kv => Int(md, kv.Key)) : new Dictionary<string, int>(),
            };
        }

        public async Task<BackendResult> BuyWithTestPayment(string productId)
        {
            var order = await Send("POST", "/shop/order", new Dictionary<string, object?> { ["productId"] = productId });
            if (!order.Ok) return new BackendResult { Code = order.Code };
            string orderId = order.Data.TryGetValue("orderId", out var o) ? o as string ?? "" : "";
            return await Simple("/shop/dev/pay", new Dictionary<string, object?> { ["orderId"] = orderId });
        }

        public Task<BackendResult> ClaimMonthCard(string cardId) =>
            Simple("/shop/month-card/claim", new Dictionary<string, object?> { ["productId"] = cardId });

        public Task<BackendResult> ClaimGrowthFund(int level) =>
            Simple("/shop/growth-fund/claim", new Dictionary<string, object?> { ["points"] = (long)level });

        public Task<BackendResult> SweepDungeon(string dungeonId, int count) =>
            Simple("/dungeon/sweep", new Dictionary<string, object?> { ["id"] = dungeonId, ["count"] = (long)count });

        public Task<BackendResult> ClaimQuest(string questId) =>
            Simple("/quest/claim", new Dictionary<string, object?> { ["questId"] = questId });

        public Task<BackendResult> ClaimMilestone(int points) =>
            Simple("/quest/milestone", new Dictionary<string, object?> { ["points"] = (long)points });

        public async Task<PullOutcomeResult> Pull(string poolId, int count)
        {
            var r = await Send("POST", "/gacha/pull", new Dictionary<string, object?> { ["poolId"] = poolId, ["count"] = (long)count });
            var result = new PullOutcomeResult { Ok = r.Ok, Code = r.Code };
            if (r.Ok && r.Data.TryGetValue("results", out var rs) && rs is List<object?> list)
            {
                foreach (var item in list)
                {
                    if (!(item is Dictionary<string, object?> o)) continue;
                    Enum.TryParse(o.TryGetValue("rarity", out var ra) ? ra as string : "R", out Rarity rarity);
                    result.Results.Add(new PullResult
                    {
                        HeroId = o.TryGetValue("heroId", out var h) ? h as string ?? "" : "",
                        Rarity = rarity,
                        IsNew = o.TryGetValue("isNew", out var n) && n is true,
                        Shards = Int(o, "shards"),
                        IsUp = o.TryGetValue("isUp", out var u) && u is true,
                        FromPity = o.TryGetValue("fromPity", out var f) && f is true,
                    });
                }
            }
            return result;
        }

        private async Task<BackendResult> Simple(string path, Dictionary<string, object?> body)
        {
            var r = await Send("POST", path, body);
            return new BackendResult { Ok = r.Ok, Code = r.Code };
        }

        public Task<BackendResult> LevelUp(string heroId) =>
            Simple("/hero/levelup", new Dictionary<string, object?> { ["heroId"] = heroId });

        public Task<BackendResult> Enhance(string heroId, string cardId) =>
            Simple("/hero/enhance", new Dictionary<string, object?> { ["heroId"] = heroId, ["cardId"] = cardId });

        public Task<BackendResult> Breakthrough(string heroId) =>
            Simple("/hero/breakthrough", new Dictionary<string, object?> { ["heroId"] = heroId });

        public async Task<SweepOutcome> Sweep(string stageId, int count)
        {
            var r = await Send("POST", "/stage/sweep", new Dictionary<string, object?> { ["id"] = stageId, ["count"] = (long)count });
            return new SweepOutcome
            {
                Ok = r.Ok, Code = r.Code,
                Exp = Int(r.Data, "exp"), Gold = Int(r.Data, "gold"), LevelsGained = Int(r.Data, "levelsGained"),
            };
        }
    }
}
