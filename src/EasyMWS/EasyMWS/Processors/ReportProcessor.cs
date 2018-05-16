﻿using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using MountainWarehouse.EasyMWS.CallbackLogic;
using MountainWarehouse.EasyMWS.Client;
using MountainWarehouse.EasyMWS.Data;
using MountainWarehouse.EasyMWS.Enums;
using MountainWarehouse.EasyMWS.Logging;
using MountainWarehouse.EasyMWS.Model;
using MountainWarehouse.EasyMWS.Services;
using MountainWarehouse.EasyMWS.WebService.MarketplaceWebService;
using Newtonsoft.Json;

namespace MountainWarehouse.EasyMWS.Processors
{
	internal class ReportProcessor : IQueueingProcessor<ReportRequestPropertiesContainer>, IReportProcessor
	{
		private readonly IReportRequestCallbackService _reportService;
		private readonly IRequestReportProcessor _requestReportProcessor;
		private readonly ICallbackActivator _callbackActivator;
		private readonly IAmazonReportService _amazonReportService;
		private readonly IEasyMwsLogger _logger;

		private readonly AmazonRegion _region;
		private readonly string _merchantId;
		private readonly EasyMwsOptions _options;

		public event EventHandler<ReportDownloadedEventArgs> ReportDownloaded;

		internal ReportProcessor(AmazonRegion region, string merchantId, EasyMwsOptions options,
			IReportRequestCallbackService reportService, IMarketplaceWebServiceClient mwsClient,
			IRequestReportProcessor requestReportProcessor, ICallbackActivator callbackActivator,
			IAmazonReportService amazonReportService, IEasyMwsLogger logger)
			: this(region, merchantId, options, mwsClient, logger)
		{
			_reportService = reportService;
			_requestReportProcessor = requestReportProcessor;
			_callbackActivator = callbackActivator;
			_amazonReportService = amazonReportService;
		}

		internal ReportProcessor(AmazonRegion region, string merchantId, EasyMwsOptions options,
			IMarketplaceWebServiceClient mwsClient, IEasyMwsLogger logger)
		{
			_region = region;
			_merchantId = merchantId;
			_options = options;
			_logger = logger;

			_callbackActivator = _callbackActivator ?? new CallbackActivator();
			_amazonReportService = _amazonReportService ?? new AmazonReportService(_options);
			_reportService = _reportService ?? new ReportRequestCallbackService(_options, _logger);
			_requestReportProcessor = _requestReportProcessor ?? new RequestReportProcessor(_region, _merchantId, mwsClient, _reportService, _amazonReportService, _logger, _options);
		}


		public void Poll()
		{
			_logger.Info("EasyMwsClient: Executing polling action for report requests.");
			try
			{
				_requestReportProcessor.CleanupReportRequests();

				RequestNextReportInQueueFromAmazon();
				_reportService.SaveChanges();

				RequestReportStatusesFromAmazon();
				_reportService.SaveChanges();

				var reportInfo = DownloadNextReportInQueueFromAmazon();
				_reportService.SaveChanges();

				if (reportInfo.stream != null)
				{
					PerformCallback(reportInfo.reportRequestCallback, reportInfo.stream);
				}
			}
			catch (Exception e)
			{
				_logger.Error(e.Message, e);
			}
		}

		private void PerformCallback(ReportRequestCallback reportRequest, Stream stream)
		{
			try
			{
				if (reportRequest.MethodName != null)
				{
					ExecuteMethodCallback(reportRequest, stream);
				}
				else
				{
					InvokeReportDownloadedEvent(reportRequest, stream);
				}
				_requestReportProcessor.RemoveFromQueue(reportRequest.Id);
				_reportService.SaveChanges();
			}
			catch (Exception e)
			{
				_logger.Error(e.Message, e);
			}
		}

		public void Queue(ReportRequestPropertiesContainer propertiesContainer, Action<Stream, object> callbackMethod, object callbackData)
		{
			try
			{
				_reportService.Create(GetSerializedReportRequestCallback(propertiesContainer, callbackMethod, callbackData));
				_reportService.SaveChanges();
			}
			catch (Exception e)
			{
				_logger.Error(e.Message, e);
			}
		}

		public void Queue(ReportRequestPropertiesContainer propertiesContainer)
		{
			Queue(propertiesContainer, null, null);
		}

		public void RequestNextReportInQueueFromAmazon()
		{
			var reportRequest = _requestReportProcessor.GetNextFromQueueOfReportsToRequest();

			if (reportRequest == null) return;
			
			try
			{
				var reportRequestId = _requestReportProcessor.RequestReportFromAmazon(reportRequest);

				reportRequest.LastRequested = DateTime.UtcNow;
				_reportService.Update(reportRequest);

				if (string.IsNullOrEmpty(reportRequestId))
				{
					_requestReportProcessor.MoveToRetryQueue(reportRequest);
					_logger.Warn($"AmazonMWS request failed for {reportRequest.RegionAndTypeComputed}");
				}
				else
				{
					_requestReportProcessor.MoveToQueueOfReportsToGenerate(reportRequest, reportRequestId);
					_logger.Info($"AmazonMWS request succeeded for {reportRequest.RegionAndTypeComputed}. ReportRequestId:'{reportRequestId}'");
				}
			}
			catch (Exception e)
			{
				_requestReportProcessor.MoveToRetryQueue(reportRequest);
				_logger.Error(e.Message, e);
			}
		}

		public void RequestReportStatusesFromAmazon()
		{
			try
			{
				var pendingReportsRequestIds = _requestReportProcessor.GetAllPendingReportFromQueue().ToList();

				if (!pendingReportsRequestIds.Any()) return;

				var reportRequestStatuses = _requestReportProcessor.GetReportProcessingStatusesFromAmazon(pendingReportsRequestIds, _merchantId);

				_requestReportProcessor.QueueReportsAccordingToProcessingStatus(reportRequestStatuses);
			}
			catch (Exception e)
			{
				_logger.Error(e.Message, e);
			}
		}

		public (ReportRequestCallback reportRequestCallback, Stream stream) DownloadNextReportInQueueFromAmazon()
		{
			var reportToDownload = _requestReportProcessor.GetNextFromQueueOfReportsToDownload();
			if (reportToDownload == null) return (null, null);

			try
			{
				var stream = _requestReportProcessor.DownloadGeneratedReportFromAmazon(reportToDownload);

				return (reportToDownload, stream);
			}
			catch (Exception e)
			{
				_logger.Error(e.Message, e);
				return (null, null);
			}
		}

		public void ExecuteMethodCallback(ReportRequestCallback reportRequest, Stream stream)
		{
			_logger.Info(
				$"Attempting to perform method callback for the next downloaded report in queue : {reportRequest.RegionAndTypeComputed}.");

			var callback = new Callback(reportRequest.TypeName, reportRequest.MethodName,
				reportRequest.Data, reportRequest.DataTypeName);

			_callbackActivator.CallMethod(callback, stream);
		}

		private void InvokeReportDownloadedEvent(ReportRequestCallback reportRequest, Stream stream)
		{
			var feedPropertiesContainer = reportRequest.GetPropertiesContainer();
			ReportDownloaded?.Invoke(this, new ReportDownloadedEventArgs
			{
				ReportContent = stream,
				AmazonRegion = reportRequest.AmazonRegion,
				MerchantId = reportRequest.MerchantId,
				GeneratedReportId = reportRequest.GeneratedReportId,
				ReportType = feedPropertiesContainer.ReportType
			});
		}

		private ReportRequestCallback GetSerializedReportRequestCallback(
			ReportRequestPropertiesContainer reportRequestContainer, Action<Stream, object> callbackMethod, object callbackData)
		{
			if (reportRequestContainer == null) throw new ArgumentNullException();
			
			var serializedPropertiesContainer = JsonConvert.SerializeObject(reportRequestContainer);

			var reportRequest = new ReportRequestCallback(serializedPropertiesContainer)
			{
				AmazonRegion = _region,
				MerchantId = _merchantId,
				LastRequested = DateTime.MinValue,
				ContentUpdateFrequency = reportRequestContainer.UpdateFrequency,
				RequestReportId = null,
				GeneratedReportId = null,
				RequestRetryCount = 0,
				ReportType = reportRequestContainer.ReportType
			};

			if (callbackMethod != null)
			{
				var serializedCallback = _callbackActivator.SerializeCallback(callbackMethod, callbackData);
				reportRequest.Data = serializedCallback.Data;
				reportRequest.TypeName = serializedCallback.TypeName;
				reportRequest.MethodName = serializedCallback.MethodName;
				reportRequest.DataTypeName = serializedCallback.DataTypeName;
			}

			return reportRequest;
		}
	}
}
