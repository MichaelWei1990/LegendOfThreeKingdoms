using LegendOfThreeKingdoms.Core.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace core.Tests.Identity;

[TestClass]
public sealed class RoleDistributionTests
{
    [TestMethod]
    public void GetDefaultDistribution_4Players_ReturnsCorrectDistribution()
    {
        // Arrange
        var table = new RoleDistributionTable();

        // Act
        var distribution = table.GetDefaultDistribution(4);

        // Assert
        Assert.IsNotNull(distribution);
        Assert.AreEqual(1, distribution.LordCount);
        Assert.AreEqual(1, distribution.LoyalistCount);
        Assert.AreEqual(1, distribution.RebelCount);
        Assert.AreEqual(1, distribution.RenegadeCount);
        Assert.AreEqual(4, distribution.TotalCount);
    }

    [TestMethod]
    public void GetDefaultDistribution_5Players_ReturnsCorrectDistribution()
    {
        // Arrange
        var table = new RoleDistributionTable();

        // Act
        var distribution = table.GetDefaultDistribution(5);

        // Assert
        Assert.IsNotNull(distribution);
        Assert.AreEqual(1, distribution.LordCount);
        Assert.AreEqual(1, distribution.LoyalistCount);
        Assert.AreEqual(2, distribution.RebelCount);
        Assert.AreEqual(1, distribution.RenegadeCount);
        Assert.AreEqual(5, distribution.TotalCount);
    }

    [TestMethod]
    public void GetDefaultDistribution_6Players_ReturnsCorrectDistribution()
    {
        // Arrange
        var table = new RoleDistributionTable();

        // Act
        var distribution = table.GetDefaultDistribution(6);

        // Assert
        Assert.IsNotNull(distribution);
        Assert.AreEqual(1, distribution.LordCount);
        Assert.AreEqual(1, distribution.LoyalistCount);
        Assert.AreEqual(3, distribution.RebelCount);
        Assert.AreEqual(1, distribution.RenegadeCount);
        Assert.AreEqual(6, distribution.TotalCount);
    }

    [TestMethod]
    public void GetDefaultDistribution_10Players_ReturnsCorrectDistribution()
    {
        // Arrange
        var table = new RoleDistributionTable();

        // Act
        var distribution = table.GetDefaultDistribution(10);

        // Assert
        Assert.IsNotNull(distribution);
        Assert.AreEqual(1, distribution.LordCount);
        Assert.AreEqual(3, distribution.LoyalistCount);
        Assert.AreEqual(4, distribution.RebelCount);
        Assert.AreEqual(2, distribution.RenegadeCount);
        Assert.AreEqual(10, distribution.TotalCount);
    }

    [TestMethod]
    public void GetDefaultDistribution_InvalidPlayerCount_ReturnsNull()
    {
        // Arrange
        var table = new RoleDistributionTable();

        // Act
        var distribution3 = table.GetDefaultDistribution(3);
        var distribution11 = table.GetDefaultDistribution(11);

        // Assert
        Assert.IsNull(distribution3);
        Assert.IsNull(distribution11);
    }

    [TestMethod]
    public void GetVariants_6Players_ReturnsVariantWithTwoRenegades()
    {
        // Arrange
        var table = new RoleDistributionTable();

        // Act
        var variants = table.GetVariants(6);

        // Assert
        Assert.IsTrue(variants.Count > 0);
        var variant = variants[0];
        Assert.AreEqual(1, variant.LordCount);
        Assert.AreEqual(1, variant.LoyalistCount);
        Assert.AreEqual(2, variant.RebelCount);
        Assert.AreEqual(2, variant.RenegadeCount);
        Assert.AreEqual(6, variant.TotalCount);
    }

    [TestMethod]
    public void GetVariants_8Players_ReturnsVariantWithTwoRenegades()
    {
        // Arrange
        var table = new RoleDistributionTable();

        // Act
        var variants = table.GetVariants(8);

        // Assert
        Assert.IsTrue(variants.Count > 0);
        var variant = variants[0];
        Assert.AreEqual(1, variant.LordCount);
        Assert.AreEqual(1, variant.LoyalistCount);
        Assert.AreEqual(3, variant.RebelCount);
        Assert.AreEqual(2, variant.RenegadeCount);
        Assert.AreEqual(8, variant.TotalCount);
    }

    [TestMethod]
    public void GetVariants_InvalidPlayerCount_ReturnsEmpty()
    {
        // Arrange
        var table = new RoleDistributionTable();

        // Act
        var variants = table.GetVariants(4);

        // Assert
        Assert.AreEqual(0, variants.Count);
    }
}

