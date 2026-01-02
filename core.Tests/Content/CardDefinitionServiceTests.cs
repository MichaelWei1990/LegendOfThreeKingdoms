using LegendOfThreeKingdoms.Core.Content;
using LegendOfThreeKingdoms.Core.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests.Content;

[TestClass]
public sealed class CardDefinitionServiceTests
{
    private ICardDefinitionService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new BasicCardDefinitionService();
    }

    /// <summary>
    /// Verifies that basic card definitions are correctly mapped.
    /// </summary>
    [TestMethod]
    public void CardDefinitionService_BasicCards_AreCorrectlyMapped()
    {
        // Test Slash
        var slash = _service.GetDefinition("Base.Slash");
        Assert.IsNotNull(slash, "Slash definition should exist");
        Assert.AreEqual("杀", slash.Name);
        Assert.AreEqual(CardType.Basic, slash.CardType);
        Assert.AreEqual(CardSubType.Slash, slash.CardSubType);

        // Test Dodge
        var dodge = _service.GetDefinition("Base.Dodge");
        Assert.IsNotNull(dodge, "Dodge definition should exist");
        Assert.AreEqual("闪", dodge.Name);
        Assert.AreEqual(CardType.Basic, dodge.CardType);
        Assert.AreEqual(CardSubType.Dodge, dodge.CardSubType);

        // Test Peach
        var peach = _service.GetDefinition("Base.Peach");
        Assert.IsNotNull(peach, "Peach definition should exist");
        Assert.AreEqual("桃", peach.Name);
        Assert.AreEqual(CardType.Basic, peach.CardType);
        Assert.AreEqual(CardSubType.Peach, peach.CardSubType);
    }

    /// <summary>
    /// Verifies that trick card definitions are correctly mapped.
    /// </summary>
    [TestMethod]
    public void CardDefinitionService_TrickCards_AreCorrectlyMapped()
    {
        // Test immediate trick cards
        var wuzhong = _service.GetDefinition("Trick.WuzhongShengyou");
        Assert.IsNotNull(wuzhong, "WuzhongShengyou definition should exist");
        Assert.AreEqual("无中生有", wuzhong.Name);
        Assert.AreEqual(CardType.Trick, wuzhong.CardType);
        Assert.AreEqual(CardSubType.WuzhongShengyou, wuzhong.CardSubType);

        var guohe = _service.GetDefinition("Trick.GuoheChaiqiao");
        Assert.IsNotNull(guohe, "GuoheChaiqiao definition should exist");
        Assert.AreEqual("过河拆桥", guohe.Name);
        Assert.AreEqual(CardType.Trick, guohe.CardType);
        Assert.AreEqual(CardSubType.GuoheChaiqiao, guohe.CardSubType);

        // Test delayed trick cards
        var lebusishu = _service.GetDefinition("Trick.Lebusishu");
        Assert.IsNotNull(lebusishu, "Lebusishu definition should exist");
        Assert.AreEqual("乐不思蜀", lebusishu.Name);
        Assert.AreEqual(CardType.Trick, lebusishu.CardType);
        Assert.AreEqual(CardSubType.Lebusishu, lebusishu.CardSubType);

        var shandian = _service.GetDefinition("Trick.Shandian");
        Assert.IsNotNull(shandian, "Shandian definition should exist");
        Assert.AreEqual("闪电", shandian.Name);
        Assert.AreEqual(CardType.Trick, shandian.CardType);
        Assert.AreEqual(CardSubType.Shandian, shandian.CardSubType);
    }

    /// <summary>
    /// Verifies that equipment card definitions are correctly mapped.
    /// </summary>
    [TestMethod]
    public void CardDefinitionService_EquipmentCards_AreCorrectlyMapped()
    {
        // Test weapons
        var zhugeliannu = _service.GetDefinition("Equip.Zhugeliannu");
        Assert.IsNotNull(zhugeliannu, "Zhugeliannu definition should exist");
        Assert.AreEqual("诸葛连弩", zhugeliannu.Name);
        Assert.AreEqual(CardType.Equip, zhugeliannu.CardType);
        Assert.AreEqual(CardSubType.Weapon, zhugeliannu.CardSubType);

        // Test armor
        var bagua = _service.GetDefinition("Equip.BaguaZhen");
        Assert.IsNotNull(bagua, "BaguaZhen definition should exist");
        Assert.AreEqual("八卦阵", bagua.Name);
        Assert.AreEqual(CardType.Equip, bagua.CardType);
        Assert.AreEqual(CardSubType.Armor, bagua.CardSubType);

        // Test offensive horses
        var chitu = _service.GetDefinition("Equip.Chitu");
        Assert.IsNotNull(chitu, "Chitu definition should exist");
        Assert.AreEqual("赤兔", chitu.Name);
        Assert.AreEqual(CardType.Equip, chitu.CardType);
        Assert.AreEqual(CardSubType.OffensiveHorse, chitu.CardSubType);

        // Test defensive horses
        var dilu = _service.GetDefinition("Equip.Dilu");
        Assert.IsNotNull(dilu, "Dilu definition should exist");
        Assert.AreEqual("的卢", dilu.Name);
        Assert.AreEqual(CardType.Equip, dilu.CardType);
        Assert.AreEqual(CardSubType.DefensiveHorse, dilu.CardSubType);
    }

    /// <summary>
    /// Verifies that HasDefinition correctly identifies existing and non-existing definitions.
    /// </summary>
    [TestMethod]
    public void CardDefinitionService_HasDefinition_WorksCorrectly()
    {
        // Existing definitions
        Assert.IsTrue(_service.HasDefinition("Base.Slash"), "Base.Slash should exist");
        Assert.IsTrue(_service.HasDefinition("Trick.WuzhongShengyou"), "Trick.WuzhongShengyou should exist");
        Assert.IsTrue(_service.HasDefinition("Equip.Zhugeliannu"), "Equip.Zhugeliannu should exist");

        // Non-existing definitions
        Assert.IsFalse(_service.HasDefinition("NonExistent.Card"), "NonExistent.Card should not exist");
        Assert.IsFalse(_service.HasDefinition(""), "Empty string should not exist");
        Assert.IsFalse(_service.HasDefinition(null!), "Null should not exist");
    }

    /// <summary>
    /// Verifies that GetDefinition returns null for non-existing definitions.
    /// </summary>
    [TestMethod]
    public void CardDefinitionService_GetDefinition_ReturnsNullForNonExistent()
    {
        Assert.IsNull(_service.GetDefinition("NonExistent.Card"), "Non-existent card should return null");
        Assert.IsNull(_service.GetDefinition(""), "Empty string should return null");
        Assert.IsNull(_service.GetDefinition(null!), "Null should return null");
    }

    /// <summary>
    /// Verifies that all standard edition card types are covered.
    /// </summary>
    [TestMethod]
    public void CardDefinitionService_AllStandardEditionCards_AreDefined()
    {
        // Basic cards
        Assert.IsTrue(_service.HasDefinition("Base.Slash"), "Base.Slash should be defined");
        Assert.IsTrue(_service.HasDefinition("Base.Dodge"), "Base.Dodge should be defined");
        Assert.IsTrue(_service.HasDefinition("Base.Peach"), "Base.Peach should be defined");

        // Immediate trick cards
        Assert.IsTrue(_service.HasDefinition("Trick.GuoheChaiqiao"), "Trick.GuoheChaiqiao should be defined");
        Assert.IsTrue(_service.HasDefinition("Trick.ShunshouQianyang"), "Trick.ShunshouQianyang should be defined");
        Assert.IsTrue(_service.HasDefinition("Trick.WuzhongShengyou"), "Trick.WuzhongShengyou should be defined");
        Assert.IsTrue(_service.HasDefinition("Trick.Harvest"), "Trick.Harvest should be defined");
        Assert.IsTrue(_service.HasDefinition("Trick.WanjianQifa"), "Trick.WanjianQifa should be defined");
        Assert.IsTrue(_service.HasDefinition("Trick.NanmanRushin"), "Trick.NanmanRushin should be defined");
        Assert.IsTrue(_service.HasDefinition("Trick.Duel"), "Trick.Duel should be defined");
        Assert.IsTrue(_service.HasDefinition("Trick.JieDaoShaRen"), "Trick.JieDaoShaRen should be defined");
        Assert.IsTrue(_service.HasDefinition("Trick.Wuxiekeji"), "Trick.Wuxiekeji should be defined");

        // Delayed trick cards
        Assert.IsTrue(_service.HasDefinition("Trick.Lebusishu"), "Trick.Lebusishu should be defined");
        Assert.IsTrue(_service.HasDefinition("Trick.Shandian"), "Trick.Shandian should be defined");

        // Equipment - Weapons
        Assert.IsTrue(_service.HasDefinition("Equip.Zhugeliannu"), "Equip.Zhugeliannu should be defined");
        Assert.IsTrue(_service.HasDefinition("Equip.CixiongShuanggujian"), "Equip.CixiongShuanggujian should be defined");
        Assert.IsTrue(_service.HasDefinition("Equip.HanbingJian"), "Equip.HanbingJian should be defined");
        Assert.IsTrue(_service.HasDefinition("Equip.QinglongYanyueDao"), "Equip.QinglongYanyueDao should be defined");
        Assert.IsTrue(_service.HasDefinition("Equip.QinggangJian"), "Equip.QinggangJian should be defined");
        Assert.IsTrue(_service.HasDefinition("Equip.QilinGong"), "Equip.QilinGong should be defined");
        Assert.IsTrue(_service.HasDefinition("Equip.ZhangbaShemao"), "Equip.ZhangbaShemao should be defined");
        Assert.IsTrue(_service.HasDefinition("Equip.Guanshifu"), "Equip.Guanshifu should be defined");
        Assert.IsTrue(_service.HasDefinition("Equip.FangtianHuaji"), "Equip.FangtianHuaji should be defined");

        // Equipment - Armor
        Assert.IsTrue(_service.HasDefinition("Equip.BaguaZhen"), "Equip.BaguaZhen should be defined");
        Assert.IsTrue(_service.HasDefinition("Equip.RenwangDun"), "Equip.RenwangDun should be defined");

        // Equipment - Horses
        Assert.IsTrue(_service.HasDefinition("Equip.Jueying"), "Equip.Jueying should be defined");
        Assert.IsTrue(_service.HasDefinition("Equip.Chitu"), "Equip.Chitu should be defined");
        Assert.IsTrue(_service.HasDefinition("Equip.ZhaohuangFeidian"), "Equip.ZhaohuangFeidian should be defined");
        Assert.IsTrue(_service.HasDefinition("Equip.Dawan"), "Equip.Dawan should be defined");
        Assert.IsTrue(_service.HasDefinition("Equip.Dilu"), "Equip.Dilu should be defined");
        Assert.IsTrue(_service.HasDefinition("Equip.Zixing"), "Equip.Zixing should be defined");
    }
}
