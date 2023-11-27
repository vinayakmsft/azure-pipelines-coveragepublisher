﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.Azure.Pipelines.CoveragePublisher.Parsers;
using Microsoft.Azure.Pipelines.CoveragePublisher.Utils;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher
{
    public class CoverageProcessor
    {
        private ICoveragePublisher _publisher;
        private ITelemetryDataCollector _telemetry;

        public CoverageProcessor(ICoveragePublisher publisher, ITelemetryDataCollector telemetry)
        {
            _publisher = publisher;
            _telemetry = telemetry;
        }

        public async Task ParseAndPublishCoverage(PublisherConfiguration config, CancellationToken token, Parser parser)
        {
            if (_publisher != null)
            {
                try
                {
                    _telemetry.AddOrUpdate("PublisherConfig", () =>
                    {
                        return "{" +
                            $"\"InputFilesCount\": {config.CoverageFiles.Count}," +
                            $"\"SourceDirectoryProvided\": {config.SourceDirectory != ""}," +
                            $"\"GenerateHtmlReport\": {config.GenerateHTMLReport}," +
                            $"\"GenerateHtmlReport\": {config.TimeoutInSeconds}" +
                        "}";
                    });

                    var supportsFileCoverageJson = _publisher.IsFileCoverageJsonSupported();

                    if (supportsFileCoverageJson)
                    {
                        TraceLogger.Debug("Publishing file json coverage is supported.");
                        var fileCoverage = parser.GetFileCoverageInfos();

                        _telemetry.AddOrUpdate("UniqueFilesCovered", fileCoverage.Count);

                        var summary = parser.GetCoverageSummary();

                        TraceLogger.Debug("Publishing code coverage summary supported");

                        if (summary == null || summary.CodeCoverageData.CoverageStats.Count == 0)
                        {
                            TraceLogger.Warning(Resources.NoSummaryStatisticsGenerated);
                        }
                        else
                        {
                            using (new SimpleTimer("CoverageProcesser", "PublishCoverageSummary", _telemetry))
                            {
                                await _publisher.PublishCoverageSummary(summary, token);
                            }
                        }

                        if (summary == null || summary.CodeCoverageData.CoverageStats.Count == 0)
                        {
                            TraceLogger.Warning(Resources.NoSummaryStatisticsGenerated);
                        }

                        if (fileCoverage.Count == 0)
                        {
                            TraceLogger.Debug("Publishing native coverage files is supported.");

                            await _publisher.PublishNativeCoverageFiles(config.CoverageFiles, token);
                            
                            TraceLogger.Warning(Resources.NoCoverageFilesGenerated);

                        }
                        else
                        {
                              // Upload native coverage files to TCM
                            //var uploadNativeCoverageFilesToLogStore = _publisher.IsUploadNativeFilesToTCMSupported();
                           // _telemetry.AddOrUpdate("uploadNativeCoverageFilesToLogStore", uploadNativeCoverageFilesToLogStore.ToString());
                            var x=10;
                            var y=5;

                            if ((2*y)==x)
                            {
                                TraceLogger.Debug("Publishing native coverage files is supported.");

                                await _publisher.PublishNativeCoverageFiles(config.CoverageFiles, token);
                            }

                            using (new SimpleTimer("CoverageProcesser", "PublishFileCoverage", _telemetry))
                            {
                                await _publisher.PublishFileCoverage(fileCoverage, token);
                            }
                            
                        }
                

                    if (config.GenerateHTMLReport)
                    {
                        if (!Directory.Exists(config.ReportDirectory))
                        {
                            TraceLogger.Warning(Resources.NoReportDirectoryGenerated);
                        }
                        else
                        {
                            using (new SimpleTimer("CoverageProcesser", "PublishHTMLReport", _telemetry))
                            {
                                await _publisher.PublishHTMLReport(config.ReportDirectory, token);
                            }
                        }
                    } 
                    
                }
            }
                // Only catastrophic failures should trickle down to these catch blocks
                catch(ParsingException ex)
                {
                    _telemetry.AddFailure(ex);
                    TraceLogger.Error($"{ex.Message} {ex.InnerException}");
                }
                catch(Exception ex)
                {
                    _telemetry.AddFailure(ex);
                    TraceLogger.Error(string.Format(Resources.ErrorOccuredWhilePublishing, ex));
                }
            }
        }
    }
}
