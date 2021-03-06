﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Moq;
using MountainWarehouse.EasyMWS.Client;
using MountainWarehouse.EasyMWS.Data;
using MountainWarehouse.EasyMWS.Enums;
using MountainWarehouse.EasyMWS.Helpers;
using MountainWarehouse.EasyMWS.Logging;
using MountainWarehouse.EasyMWS.Model;
using MountainWarehouse.EasyMWS.Processors;
using MountainWarehouse.EasyMWS.WebService.MarketplaceWebService;
using MountainWarehouse.EasyMWS.WebService.MarketplaceWebService.Model;
using NUnit.Framework;

namespace EasyMWS.Tests.EndToEnd
{
    public class FeedSubmissionTests
    {
	    private EasyMwsContext _dbContext;
	    private const string _testEntriesIdentifier = "TEST";

	    private IEasyMwsClient _easyMwsClient;
	    private readonly AmazonRegion _region = AmazonRegion.Europe;
	    private readonly string _merchantId = "testMerchantId";
        private readonly string _mwsAuthToken = "testMwsAuthToken";
        private EasyMwsOptions _options = new EasyMwsOptions();

	    private Mock<IEasyMwsLogger> _loggerMock;
	    private Mock<IMarketplaceWebServiceClient> _mwsClientMock;

	    private static Stream _actualFeedSubmissionReportContent;

		[SetUp]
	    public void SetUp()
	    {
		    _options = new EasyMwsOptions();
		    _dbContext = new EasyMwsContext();
		    _actualFeedSubmissionReportContent = null;

		    _loggerMock = new Mock<IEasyMwsLogger>();
		    _mwsClientMock = new Mock<IMarketplaceWebServiceClient>();
		    var reportProcessor = new Mock<IReportQueueingProcessor>();

		    var feedProcessor = new FeedProcessor(_region, _merchantId, _mwsAuthToken, _options, _mwsClientMock.Object, _loggerMock.Object);

		    _easyMwsClient = new EasyMwsClient(AmazonRegion.Europe, _merchantId, _mwsAuthToken, "test", "test", reportProcessor.Object,
			    feedProcessor, _loggerMock.Object, _options);
	    }

	    [TearDown]
	    public void TearDown()
	    {
		    var testReportEntries = _dbContext.FeedSubmissionEntries.Where(rre => rre.FeedType.StartsWith(_testEntriesIdentifier));
		    _dbContext.FeedSubmissionEntries.RemoveRange(testReportEntries);
		    _dbContext.SaveChanges();

		    _dbContext.Dispose();
	    }

		[Test]
		[Ignore("This should only be ran manually in debug mode, in order to check for potential memory leaks with MemoryProfiler.")]
		public void Test_ContinouslyInitializingClientAndPolling_ShouldNotCauseMemoryLeaks()
		{
			while (true)
			{
				_easyMwsClient = new EasyMwsClient(AmazonRegion.Europe, "MerchantId", "test", "test", "test", _loggerMock.Object, _options);
				_easyMwsClient.Poll();
			}
		}

		[Test]
	    public void
		    GivenQueuingReportAndPolling_WhenAllDataIsValid_TheReportIsDownloaded_AndIsReturnedInTheCallbackMethodAlongWithTheCallbackData()
	    {
		    // arrange
		    var validFeedType = $"{_testEntriesIdentifier}_VALID_FEED_TYPE_";
		    var validFeedContent = "This is some test content. Und die Katze läuft auf der Straße.";

		    var expectedFeedSubmissionId = "test feed submission Id";
		    var expectedFeedProcessingStatus = "_DONE_";
		    var feedSubmissionContainer = GenerateFeedContainer(validFeedContent, validFeedType);
		    var expectedCallbackData = ("test", "callback", "data");
		    var expectedSubmissionReportContent = "Test submission report content. Und die Katze läuft auf der Straße.";
		    Setup_SubmitFeed_Returns_FeedSubmissionIdWasGenerated(expectedFeedSubmissionId, validFeedType);
		    Setup_GetFeedSubmissionList_Returns_FeedSubmittedSuccessfully(expectedFeedSubmissionId, expectedFeedProcessingStatus);
		    Setup_GetFeedSubmissionResult_Returns_SubmissionReportContentStream(expectedFeedSubmissionId, expectedSubmissionReportContent);
		    _actualFeedSubmissionReportContent = null;
            var feedUploadedEventNumberOfInvocations = 0;
            ReadOnlyDictionary<string, object> targetEventArgs = null;

            _easyMwsClient.FeedUploaded += (s, e) =>
            {
                feedUploadedEventNumberOfInvocations++;
                targetEventArgs = e.TargetHandlerArgs;
                _actualFeedSubmissionReportContent = e.ProcessingReportContent;
            };

		    // act - queue report
		    _easyMwsClient.QueueFeed(feedSubmissionContainer, "targetEventId", new Dictionary<string, object> { { "key1", "value1" }, { "key2", "value2" } });

		    // act - Poll report request process, in order for the queuedReport delegate to be invoked.
		    _easyMwsClient.Poll();

            // assert - callback was invoked successfully with expected content.
            Assert.AreEqual(2, targetEventArgs.Count);
            Assert.IsTrue(targetEventArgs.ContainsKey("key1"));
            Assert.IsTrue(targetEventArgs.ContainsKey("key2"));
            Assert.IsNotNull(targetEventArgs.FirstOrDefault(kvp => kvp.Value == "value1"));
            Assert.IsNotNull(targetEventArgs.FirstOrDefault(kvp => kvp.Value == "value2"));
            Assert.AreEqual(1, feedUploadedEventNumberOfInvocations);
            Assert.AreEqual(expectedSubmissionReportContent, _actualFeedSubmissionReportContent == null ? null : new StreamReader(_actualFeedSubmissionReportContent).ReadToEnd());
		    _mwsClientMock.Verify(mws => mws.SubmitFeed(It.IsAny<SubmitFeedRequest>()), Times.Once);
		    _mwsClientMock.Verify(mws => mws.GetFeedSubmissionList(It.IsAny<GetFeedSubmissionListRequest>()), Times.Once);
		    _mwsClientMock.Verify(mws => mws.GetFeedSubmissionResult(It.IsAny<GetFeedSubmissionResultRequest>()), Times.Once);

		    var dbEntry = _dbContext.ReportRequestEntries.FirstOrDefault(rre => rre.ReportType == validFeedType);
		    Assert.IsNull(dbEntry);
	    }

	    private void Setup_SubmitFeed_Returns_FeedSubmissionIdWasGenerated(string expectedFeedSubmissionId, string feedType)
	    {
		    var response = new SubmitFeedResponse
			{
			    ResponseHeaderMetadata = new ResponseHeaderMetadata("requestId", "responseContext", "timestamp"),
			    SubmitFeedResult = new SubmitFeedResult() { FeedSubmissionInfo = new FeedSubmissionInfo() { FeedSubmissionId = expectedFeedSubmissionId } }
		    };

		    _mwsClientMock
			    .Setup(mws => mws.SubmitFeed(It.Is<SubmitFeedRequest>(rrr => rrr.FeedType == feedType && rrr.Merchant == _merchantId)))
			    .Returns(response);
	    }

	    private void Setup_GetFeedSubmissionList_Returns_FeedSubmittedSuccessfully(string expectedFeedSubmissionId, string expectedFeedProcessingStatus)
	    {
		    var response = new GetFeedSubmissionListResponse
			{
			    ResponseHeaderMetadata = new ResponseHeaderMetadata("requestId", "responseContext", "timestamp"),
			    GetFeedSubmissionListResult = new GetFeedSubmissionListResult()
			    {
				    FeedSubmissionInfo = new List<FeedSubmissionInfo>
				    {
					    new FeedSubmissionInfo
						{
							FeedSubmissionId = expectedFeedSubmissionId,
							FeedProcessingStatus = expectedFeedProcessingStatus
					    }
				    }
			    }
		    };

		    _mwsClientMock
			    .Setup(mws => mws.GetFeedSubmissionList(It.Is<GetFeedSubmissionListRequest>(rrlr => rrlr.FeedSubmissionIdList.Id.Contains(expectedFeedSubmissionId) && rrlr.Merchant == _merchantId)))
			    .Returns(response);
	    }

	    private void Setup_GetFeedSubmissionResult_Returns_SubmissionReportContentStream(string expectedFeedSubmissionId, string submissionReportContent)
	    {
		    var stream = StreamHelper.CreateMemoryStream(submissionReportContent);
		    var validMd5Hash = MD5ChecksumHelper.ComputeHashForAmazon(stream);
		    var response = new GetFeedSubmissionResultResponse
			{
			    ResponseHeaderMetadata = new ResponseHeaderMetadata("requestId", "responseContext", "timestamp"),
				GetFeedSubmissionResultResult = new GetFeedSubmissionResultResult() { ContentMD5 = validMd5Hash }
		    };

		    _mwsClientMock
			    .Setup(mws => mws.GetFeedSubmissionResult(It.Is<GetFeedSubmissionResultRequest>(grr => grr.FeedSubmissionId == expectedFeedSubmissionId && grr.Merchant == _merchantId)))
			    .Callback<GetFeedSubmissionResultRequest>(request => { stream.CopyTo(request.FeedSubmissionResult); })
			    .Returns(response);
	    }

	    private FeedSubmissionPropertiesContainer GenerateFeedContainer(string feedContent, string feedType)
	    {
		    return new FeedSubmissionPropertiesContainer(feedContent, feedType);
	    }
	}
}
