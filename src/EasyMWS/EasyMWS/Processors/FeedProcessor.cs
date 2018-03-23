﻿using System;
using System.IO;
using System.Linq;
using MountainWarehouse.EasyMWS.Data;
using MountainWarehouse.EasyMWS.Helpers;
using MountainWarehouse.EasyMWS.Services;
using MountainWarehouse.EasyMWS.WebService.MarketplaceWebService;
using Newtonsoft.Json;

namespace MountainWarehouse.EasyMWS.Processors
{
	internal class FeedProcessor : IQueueingProcessor<FeedSubmissionPropertiesContainer>, IFeedProcessor
	{
		private readonly IFeedSubmissionCallbackService _feedService;
		private readonly IFeedSubmissionProcessor _feedSubmissionProcessor;
		private readonly ICallbackActivator _callbackActivator;

		private readonly AmazonRegion _region;
		private readonly string _merchantId;
		private readonly EasyMwsOptions _options;

		internal FeedProcessor(AmazonRegion region, string merchantId, EasyMwsOptions options, IFeedSubmissionCallbackService feedService, IMarketplaceWebServiceClient mwsClient, IFeedSubmissionProcessor feedSubmissionProcessor, ICallbackActivator callbackActivator)
		  : this(region, merchantId, options, mwsClient)
		{
			_feedService = feedService;
			_feedSubmissionProcessor = feedSubmissionProcessor;
			_callbackActivator = callbackActivator;
		}

		internal FeedProcessor(AmazonRegion region, string merchantId, EasyMwsOptions options, IMarketplaceWebServiceClient mwsClient)
		{
			_region = region;
			_merchantId = merchantId;
			_options = options;

			_callbackActivator = _callbackActivator ?? new CallbackActivator();

			_feedService = _feedService ?? new FeedSubmissionCallbackService();
			_feedSubmissionProcessor = _feedSubmissionProcessor ?? new FeedSubmissionProcessor(mwsClient, _feedService, options);
		}

		public void Poll()
		{
			CleanUpFeedSubmissionQueue();
			SubmitNextFeedInQueueToAmazon();
			RequestFeedSubmissionStatusesFromAmazon();
			var amazonProcessingReport = RequestNextFeedSubmissionInQueueFromAmazon();

			// TODO: If feed processing report Content-MD5 hash doesn't match the hash sent by amazon, retry up to 3 times. 
			// log a warning for each hash miss-match, and recommend to the user to notify Amazon that a corrupted body was received. 

			if (MD5ChecksumHelper.IsChecksumCorrect(amazonProcessingReport.reportContent, amazonProcessingReport.contentMd5))
			{
				ExecuteCallback(amazonProcessingReport.feedSubmissionCallback, amazonProcessingReport.reportContent);
			}
			else
			{
				_feedSubmissionProcessor.MoveToRetryQueue(amazonProcessingReport.feedSubmissionCallback);
			}
			
			_feedService.SaveChanges();
		}

		public void Queue(FeedSubmissionPropertiesContainer propertiesContainer, Action<Stream, object> callbackMethod, object callbackData)
		{
			_feedService.Create(GetSerializedFeedSubmissionCallback(propertiesContainer, callbackMethod, callbackData));
			_feedService.SaveChanges();
		}

		public void CleanUpFeedSubmissionQueue()
		{
			var expiredFeedSubmissions = _feedService.GetAll()
				.Where(fscs => fscs.AmazonRegion == _region && fscs.MerchantId == _merchantId
				               && fscs.FeedSubmissionId == null
				               && fscs.SubmissionRetryCount > _options.FeedSubmissionMaxRetryCount);

			foreach (var feedSubmission in expiredFeedSubmissions)
			{
				_feedService.Delete(feedSubmission);
			}

			var expiredFeedProcessingResultRequests = _feedService.GetAll()
				.Where(fscs => fscs.AmazonRegion == _region && fscs.MerchantId == _merchantId
				               && fscs.FeedSubmissionId != null
				               && fscs.SubmissionRetryCount > _options.FeedResultFailedChecksumMaxRetryCount);

			foreach (var feedSubmission in expiredFeedProcessingResultRequests)
			{
				_feedService.Delete(feedSubmission);
			}
		}

		public void SubmitNextFeedInQueueToAmazon()
		{
			var feedSubmission = _feedSubmissionProcessor.GetNextFeedToSubmitFromQueue(_region, _merchantId);

			if (feedSubmission == null)
				return;

			var feedSubmissionId = _feedSubmissionProcessor.SubmitSingleQueuedFeedToAmazon(feedSubmission, _merchantId);

			feedSubmission.LastSubmitted = DateTime.UtcNow;
			_feedService.Update(feedSubmission);

			if (string.IsNullOrEmpty(feedSubmissionId))
			{
				_feedSubmissionProcessor.MoveToRetryQueue(feedSubmission);
			}
			else
			{
				_feedSubmissionProcessor.MoveToQueueOfSubmittedFeeds(feedSubmission, feedSubmissionId);
			}
		}

		public void RequestFeedSubmissionStatusesFromAmazon()
		{
			var submittedFeeds = _feedSubmissionProcessor.GetAllSubmittedFeeds(_region, _merchantId).ToList();

			if (!submittedFeeds.Any())
				return;

			var feedSubmissionIdList = submittedFeeds.Select(x => x.FeedSubmissionId);

			var feedSubmissionResults = _feedSubmissionProcessor.GetFeedSubmissionResults(feedSubmissionIdList, _merchantId);

			_feedSubmissionProcessor.MoveFeedsToQueuesAccordingToProcessingStatus(feedSubmissionResults);
		}

		public (FeedSubmissionCallback feedSubmissionCallback, Stream reportContent, string contentMd5) RequestNextFeedSubmissionInQueueFromAmazon()
		{
			var nextFeedWithProcessingComplete = _feedSubmissionProcessor.GetNextFeedFromProcessingCompleteQueue(_region, _merchantId);

			if (nextFeedWithProcessingComplete == null) return (null, null, null);

			var processingReportInfo = _feedSubmissionProcessor.QueryFeedProcessingReport(nextFeedWithProcessingComplete, _merchantId);

			return (nextFeedWithProcessingComplete, processingReportInfo.processingReport, processingReportInfo.md5hash);
		}

		public void ExecuteCallback(FeedSubmissionCallback feedSubmissionCallback, Stream stream)
		{
			if (feedSubmissionCallback == null || stream == null) return;

			var callback = new Callback(feedSubmissionCallback.TypeName, feedSubmissionCallback.MethodName,
			  feedSubmissionCallback.Data, feedSubmissionCallback.DataTypeName);

			_callbackActivator.CallMethod(callback, stream);

			_feedSubmissionProcessor.DequeueFeedSubmissionCallback(feedSubmissionCallback);
		}

		private FeedSubmissionCallback GetSerializedFeedSubmissionCallback(
		  FeedSubmissionPropertiesContainer propertiesContainer, Action<Stream, object> callbackMethod, object callbackData)
		{
			if (propertiesContainer == null || callbackMethod == null) throw new ArgumentNullException();
			var serializedCallback = _callbackActivator.SerializeCallback(callbackMethod, callbackData);

			return new FeedSubmissionCallback(serializedCallback)
			{
				AmazonRegion = _region,
				MerchantId = _merchantId,
				LastSubmitted = DateTime.MinValue,
				IsProcessingComplete = false,
				HasErrors = false,
				SubmissionErrorData = null,
				SubmissionRetryCount = 0,
				FeedSubmissionId = null,
				FeedSubmissionData = JsonConvert.SerializeObject(propertiesContainer),
			};
		}
	}
}