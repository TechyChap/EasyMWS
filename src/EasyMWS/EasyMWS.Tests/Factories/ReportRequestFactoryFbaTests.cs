﻿using System;
using MountainWarehouse.EasyMWS.Factories.Reports;
using MountainWarehouse.EasyMWS.Helpers;
using NUnit.Framework;

namespace EasyMWS.Tests.Factories
{
    public class ReportRequestFactoryFbaTests
    {
	    private ReportRequestFactoryFba _reportRequestFactoryFBA;

		[Test]
		public void
			GenerateRequestForReportGetAfnInventoryData_WithNoMarketplaceProvided_ReturnsRequestWithMarketplaceIdList_NotSet()
		{
			_reportRequestFactoryFBA = new ReportRequestFactoryFba();

			var reportRequest = _reportRequestFactoryFBA.GenerateRequestForReportGetAfnInventoryData();

			Assert.NotNull(reportRequest);
			Assert.IsNull(reportRequest.ReportRequest.MarketplaceIdList);
		}

		[Test]
		public void
			GenerateRequestForReportGetAfnInventoryData_WithNullMarketplaceProvided_ReturnsRequestWithMarketplaceIdList_NotSet()
		{
			_reportRequestFactoryFBA = new ReportRequestFactoryFba();

			var reportRequest = _reportRequestFactoryFBA.GenerateRequestForReportGetAfnInventoryData(null);

			Assert.NotNull(reportRequest);
			Assert.IsNull(reportRequest.ReportRequest.MarketplaceIdList);
		}

		[Test]
		public void GenerateRequestForReportGetAfnInventoryData_WithEuropeanMarketplacesProvided_ReturnsRequest()
		{
			var marketplaceGroup = new MwsMarketplaceGroup(MwsMarketplace.UK)
				.AddMarketplace(MwsMarketplace.Germany)
				.AddMarketplace(MwsMarketplace.France)
				.AddMarketplace(MwsMarketplace.Italy)
				.AddMarketplace(MwsMarketplace.Spain);
			_reportRequestFactoryFBA = new ReportRequestFactoryFba();

			var reportRequest = _reportRequestFactoryFBA.GenerateRequestForReportGetAfnInventoryData(marketplaceGroup);

			Assert.NotNull(reportRequest);
		}

		[Test]
		public void GenerateRequestForReportGetAfnInventoryData_WithAmericanMarketplacesProvided_ReturnsRequest()
		{
			var marketplaceGroup = new MwsMarketplaceGroup(MwsMarketplace.US)
				.AddMarketplace(MwsMarketplace.Canada)
				.AddMarketplace(MwsMarketplace.Mexico);
			_reportRequestFactoryFBA = new ReportRequestFactoryFba();

			var reportRequest = _reportRequestFactoryFBA.GenerateRequestForReportGetAfnInventoryData(marketplaceGroup);

			Assert.NotNull(reportRequest);
		}

		[Test]
		public void GenerateRequestForReportGetAfnInventoryData_WithNonUsOrEUMarketplaceProvided_ReturnsRequest()
		{
			var marketplaceGroup = new MwsMarketplaceGroup(MwsMarketplace.Japan);
			_reportRequestFactoryFBA = new ReportRequestFactoryFba();

			var reportRequest = _reportRequestFactoryFBA.GenerateRequestForReportGetAfnInventoryData(marketplaceGroup);

			Assert.NotNull(reportRequest);
		}

		[Test]
		public void GenerateRequestForReportGetAfnInventoryData_WithNonNullMerchant_HasMerchantSetCorrectly()
		{
			var testMerchant = "testMerchant3465";
			_reportRequestFactoryFBA = new ReportRequestFactoryFba(testMerchant);

			var reportRequest = _reportRequestFactoryFBA.GenerateRequestForReportGetAfnInventoryData();

			Assert.AreEqual(testMerchant, reportRequest.ReportRequest.Merchant);
		}

		[Test]
		public void GenerateRequestForReportGetAfnInventoryData_WithNonmWsAuthToken_HasMwsAuthTokenSetCorrectly()
		{
			var testmWsAuthToken = "mWsAuthToken3456";
			_reportRequestFactoryFBA = new ReportRequestFactoryFba(mWsAuthToken: testmWsAuthToken);

			var reportRequest = _reportRequestFactoryFBA.GenerateRequestForReportGetAfnInventoryData();

			Assert.AreEqual(testmWsAuthToken, reportRequest.ReportRequest.MWSAuthToken);
		}

		[Test]
		public void GenerateRequestForReportGetAfnInventoryData_ReturnsReportRequest_WithCorrectType()
		{
			_reportRequestFactoryFBA = new ReportRequestFactoryFba();

			var reportRequest = _reportRequestFactoryFBA.GenerateRequestForReportGetAfnInventoryData();

			Assert.AreEqual("_GET_AFN_INVENTORY_DATA_", reportRequest.ReportRequest.ReportType);
			Assert.AreEqual(ContentUpdateFrequency.NearRealTime, reportRequest.UpdateFrequency);
		}

		[Test]
		public void
			GenerateRequestForReportGetAfnInventoryDataByCountry_WithNoMarketplaceProvided_ReturnsRequestWithMarketplaceIdList_NotSet()
		{
			_reportRequestFactoryFBA = new ReportRequestFactoryFba();

			var reportRequest = _reportRequestFactoryFBA.GenerateRequestForReportGetAfnInventoryDataByCountry();

			Assert.NotNull(reportRequest);
			Assert.IsNull(reportRequest.ReportRequest.MarketplaceIdList);
		}

		[Test]
		public void
			GenerateRequestForReportGetAfnInventoryDataByCountry_WithNullMarketplaceProvided_ReturnsRequestWithMarketplaceIdList_NotSet()
		{
			_reportRequestFactoryFBA = new ReportRequestFactoryFba();

			var reportRequest = _reportRequestFactoryFBA.GenerateRequestForReportGetAfnInventoryDataByCountry(null);

			Assert.NotNull(reportRequest);
			Assert.IsNull(reportRequest.ReportRequest.MarketplaceIdList);
		}

		[Test]
		public void GenerateRequestForReportGetAfnInventoryDataByCountry_WithEuropeanMarketplacesProvided_ReturnsRequest()
		{
			var marketplaceGroup = new MwsMarketplaceGroup(MwsMarketplace.UK)
				.AddMarketplace(MwsMarketplace.Germany)
				.AddMarketplace(MwsMarketplace.France)
				.AddMarketplace(MwsMarketplace.Italy)
				.AddMarketplace(MwsMarketplace.Spain);
			_reportRequestFactoryFBA = new ReportRequestFactoryFba();

			var reportRequest = _reportRequestFactoryFBA.GenerateRequestForReportGetAfnInventoryDataByCountry(marketplaceGroup);

			Assert.NotNull(reportRequest);
		}

		[Test]
		public void
			GenerateRequestForReportGetAfnInventoryDataByCountry_WithAmericanMarketplaceProvided_ThrowsArgumentException()
		{
			var marketplaceGroup = new MwsMarketplaceGroup(MwsMarketplace.US);
			_reportRequestFactoryFBA = new ReportRequestFactoryFba();

			Assert.Throws<ArgumentException>(() =>
				_reportRequestFactoryFBA.GenerateRequestForReportGetAfnInventoryDataByCountry(marketplaceGroup));
		}

		[Test]
		public void
			GenerateRequestForReportGetAfnInventoryDataByCountry_WithNonEuMarketplaceProvided_ThrowsArgumentException()
		{
			var marketplaceGroup = new MwsMarketplaceGroup(MwsMarketplace.Japan);
			_reportRequestFactoryFBA = new ReportRequestFactoryFba();

			Assert.Throws<ArgumentException>(() =>
				_reportRequestFactoryFBA.GenerateRequestForReportGetAfnInventoryDataByCountry(marketplaceGroup));
		}

		[Test]
		public void GenerateRequestForReportGetAfnInventoryDataByCountry_WithNonNullMerchant_HasMerchantSetCorrectly()
		{
			var testMerchant = "testMerchant3465";
			_reportRequestFactoryFBA = new ReportRequestFactoryFba(testMerchant);

			var reportRequest = _reportRequestFactoryFBA.GenerateRequestForReportGetAfnInventoryDataByCountry();

			Assert.AreEqual(testMerchant, reportRequest.ReportRequest.Merchant);
		}

		[Test]
		public void GenerateRequestForReportGetAfnInventoryDataByCountry_WithNonMwsAuthToken_HasMwsAuthTokenSetCorrectly()
		{
			var testmWsAuthToken = "mWsAuthToken3456";
			_reportRequestFactoryFBA = new ReportRequestFactoryFba(mWsAuthToken: testmWsAuthToken);

			var reportRequest = _reportRequestFactoryFBA.GenerateRequestForReportGetAfnInventoryDataByCountry();

			Assert.AreEqual(testmWsAuthToken, reportRequest.ReportRequest.MWSAuthToken);
		}

		[Test]
		public void GenerateRequestForReportGetAfnInventoryDataByCountry_ReturnsReportRequest_WithCorrectType()
		{
			_reportRequestFactoryFBA = new ReportRequestFactoryFba();

			var reportRequest = _reportRequestFactoryFBA.GenerateRequestForReportGetAfnInventoryDataByCountry();

			Assert.AreEqual("_GET_AFN_INVENTORY_DATA_BY_COUNTRY_", reportRequest.ReportRequest.ReportType);
			Assert.AreEqual(ContentUpdateFrequency.NearRealTime, reportRequest.UpdateFrequency);
		}
	}
}
