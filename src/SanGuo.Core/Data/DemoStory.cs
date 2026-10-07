using System.Collections.Generic;

namespace SanGuo.Core.Data
{
    /// <summary>一句對白。<see cref="Speaker"/> 空字串＝旁白；<see cref="Portrait"/> 是武將 / 角色 id（客戶端找立繪或 3D 模型，找不到就不顯示）。</summary>
    public sealed class StoryLine
    {
        public string Speaker;
        public string Portrait;
        public string Text;

        public StoryLine(string speaker, string portrait, string text)
        {
            Speaker = speaker;
            Portrait = portrait;
            Text = text;
        }
    }

    /// <summary>第零章劇情（草案，見 docs/chapter0.md）：每關有戰前（Before）與首通後（After）兩段對白。</summary>
    public static class DemoStory
    {
        /// <summary>主角（穿越者）的對白署名；名字尚未定案，暫以第一人稱「我」帶過。</summary>
        public const string Protagonist = "我";

        private const string Narrator = "";
        private const string LiuBei = "劉備";
        private const string ZhangFei = "張飛";
        private const string GuanYu = "關羽";

        private static StoryLine N(string text) => new StoryLine(Narrator, "", text);
        private static StoryLine Me(string text) => new StoryLine(Protagonist, "", text);
        private static StoryLine Lb(string text) => new StoryLine(LiuBei, "liubei", text);
        private static StoryLine Zf(string text) => new StoryLine(ZhangFei, "zhangfei", text);
        private static StoryLine Gy(string text) => new StoryLine(GuanYu, "guanyu", text);

        /// <summary>戰前劇情。關卡編號 1–10（對應 <see cref="DemoContent.LevelNames"/>）。</summary>
        public static List<StoryLine> Before(int level)
        {
            switch (level)
            {
                case 1: return new List<StoryLine>
                {
                    N("睜開眼，頭頂是發黃的茅草屋梁。昨晚明明還在公司加班趕專案……"),
                    Me("（這是哪裡？涿縣？我記得歷史課本說過，黃巾之亂前夕，劉備還在這裡賣草鞋……）"),
                    Lb("賢弟，你醒啦！村口來了一夥流寇搶糧，鄉親們都躲起來了。"),
                    N("奇怪的是，我看得見每個賊人頭上的行動預告，手裡還多了一疊微微發光的牌。"),
                    Me("（先別管為什麼了。這些牌，好像能讓人活下來。）"),
                    Lb("你向來多謀。這回你來指揮，我在後頭照應傷者！"),
                };
                case 2: return new List<StoryLine>
                {
                    N("流寇敗退，卻留下幾個專射後排的獵戶出身的神射手。"),
                    Lb("糟了，箭專往我們身後招呼！"),
                    N("一個黑臉大漢扛著丈八長矛，從豬肉攤後走了出來。"),
                    Zf("俺乃燕人張翼德！欺負鄉親，當俺死了不成？"),
                    Me("（嘲諷！把賊人的目光全吸過來，後排就安全了。）"),
                };
                case 3: return new List<StoryLine>
                {
                    N("山路上，一名披著搶來鐵甲的悍匪擋住去路，刀槍不入。"),
                    Zf("這傢伙的皮比牛還厚！"),
                    N("路旁樹蔭下，一位面如重棗、長髯垂胸的大漢緩緩起身。"),
                    Gy("在下河東關雲長。甲再堅，也有縫隙——先破其甲。"),
                };
                case 4: return new List<StoryLine>
                {
                    N("山賊的後排站著一個搖鈴跳大神的野巫醫，傷員被他一口符水又灌回了戰線。"),
                    Me("打了半天都殺不完……優先處理會治療的那個！"),
                    Lb("可是他躲在最後面，我們打得到嗎？"),
                    Me("用射程夠遠的牌，專挑血量最低的目標。"),
                };
                case 5: return new List<StoryLine>
                {
                    N("山寨二當家提著一柄門板似的大刀，口中念念有詞，周身氣勢一層層往上疊。"),
                    Me("（他頭上的預告寫著「蓄力中」——下一回合那一刀絕對不能硬吃！）"),
                    Zf("讓俺吼他一嗓子！把他的氣勢給喝斷！"),
                };
                case 6: return new List<StoryLine>
                {
                    N("官道上，一隊馬商被山賊團團圍住。領頭的中年商人腿上受了傷。"),
                    N("商人自報姓名：中山大商人張世平。"),
                    Me("（演義裡正是他送了劉備馬匹與金銀……不能讓他死在這裡！）"),
                    Lb("護甲先給他套上，別讓箭射到他！"),
                };
                case 7: return new List<StoryLine>
                {
                    N("山賊把大批人馬擠在木柵圍成的寨子裡，連營相接，枯草遍地，秋風正急。"),
                    N("一個背著書匣的少年書生路過，笑著遞來一支火摺子。"),
                    new StoryLine("龐統", "pangtong", "在下襄陽龐士元。路過看見這些柵欄與枯草，忍不住想獻一計。"),
                    Me("單打太慢。先點火，再一口氣引爆，讓火勢蔓延整排。"),
                };
                case 8: return new List<StoryLine>
                {
                    N("山寨裡鼓聲咚咚不絕，每敲一次，就多一個小嘍囉從營房衝出來。"),
                    Me("（敲鼓的不處理掉，我們會被人海淹沒。）"),
                    N("一名白袍小將勒馬駐足，手持銀槍。"),
                    new StoryLine("趙雲", "zhaoyun", "常山趙子龍路過此地。一槍貫穿縱列，直取後排擊鼓之人，如何？"),
                };
                case 9: return new List<StoryLine>
                {
                    N("山寨大門前，一正一副兩名頭領並肩而立，身後的巫醫正在舉杯作法。"),
                    Lb("三位賢弟——前頭兩個都要蓄力，後頭還有治療的。"),
                    Me("該綜合運用了：先打斷、再集火，別讓巫醫續命。"),
                };
                case 10: return new List<StoryLine>
                {
                    N("山寨深處，占山為王的大當家「鎮山虎」坐在虎皮大椅上，身邊小嘍囉列陣兩側。"),
                    N("他的刀鋒泛著一絲詭異的暗光，不像是尋常山賊的東西。"),
                    Me("（蓄力、大招、再召喚……他的節奏一環扣一環。）"),
                    Lb("今日除了這山賊，也算為鄉親們出了一口氣！"),
                };
                default: return new List<StoryLine>();
            }
        }

        /// <summary>首通後劇情（失敗不播）。第 1–3 關結尾分別是劉備、張飛、關羽加入。</summary>
        public static List<StoryLine> After(int level)
        {
            switch (level)
            {
                case 1: return new List<StoryLine>
                {
                    N("流寇四散奔逃，村口恢復了平靜。"),
                    Lb("多虧賢弟！那些發光的牌……到底是什麼？"),
                    Me("我也說不清楚，但它能讓大家活下來。"),
                    Lb("那好。今後備願與賢弟並肩，同守鄉里！"),
                    N("【劉備加入隊伍】"),
                };
                case 2: return new List<StoryLine>
                {
                    Zf("痛快！俺老張在莊上殺豬賣酒，正嫌日子無聊！"),
                    Zf("你們這夥人有意思——俺要跟著幹！莊後有一片桃園，改天請你們喝酒！"),
                    N("【張飛加入隊伍】"),
                };
                case 3: return new List<StoryLine>
                {
                    N("悍匪倒地，露出了被他剝來的鐵甲。長髯大漢收起大刀。"),
                    Gy("在下為除鄉間惡霸逃亡至此。見諸位仗義除賊，雲長願同往。"),
                    N("【關羽加入隊伍】"),
                    N("當夜，張飛莊後的桃園，桃花正盛。"),
                    Lb("今日我劉備、關羽、張飛，不求同年同月同日生，但求同年同月同日死！"),
                    N("三人焚香結拜，我站在一旁，手中的牌忽然微微發燙，像是在回應著什麼。"),
                };
                case 4: return new List<StoryLine>
                {
                    N("巫醫的鈴鐺滾落在地。村民們說，那些符水根本不是藥，喝了只會讓人更依賴。"),
                    Lb("世道不好，才會有這種人。"),
                };
                case 5: return new List<StoryLine>
                {
                    N("二當家的大刀砸進泥地，再也舉不起來。"),
                    Zf("什麼二當家，喝一嗓子就軟了！"),
                };
                case 6: return new List<StoryLine>
                {
                    new StoryLine("張世平", "r_villager", "多謝義士相救！這些良馬、鐵與銀兩，就請收下，用來招兵買馬吧。"),
                    Lb("素昧平生，竟蒙厚贈……備必不辜負。"),
                    N("【獲得軍資：義勇軍有了第一批戰馬與兵器】"),
                };
                case 7: return new List<StoryLine>
                {
                    N("烈火燒盡了木柵，山賊的連營一夜之間成了廢墟。"),
                    new StoryLine("龐統", "pangtong", "火是好東西，但用在該用的地方才是妙計。告辭，後會有期。"),
                };
                case 8: return new List<StoryLine>
                {
                    N("鼓聲戛然而止，嘍囉們失去了指揮，紛紛丟下兵器。"),
                    new StoryLine("趙雲", "zhaoyun", "雲有主公之約在身，不能久留。他日若再相見，必不推辭。"),
                };
                case 9: return new List<StoryLine>
                {
                    N("兩名頭領接連倒下。剩下的，只有山寨深處最後的大當家。"),
                };
                case 10: return new List<StoryLine>
                {
                    N("鎮山虎倒下的瞬間，他懷中掉出一片殘破的黃色布角，上頭畫著奇異的符紋。"),
                    Lb("這是……黃色的符？"),
                    Me("（他的刀為什麼會發出那種暗光？這些山賊背後，是不是有人在給他們撐腰？）"),
                    N("此時，縣衙的快馬趕來，帶來了一張告示——「黃巾四起，州郡招募義兵，抵禦賊寇」。"),
                    N("我低頭看著手中的牌，最上面那張，浮現出一行從未見過的小字：『命牌．第一枚已歸位。』"),
                };
                default: return new List<StoryLine>();
            }
        }
    }
}
