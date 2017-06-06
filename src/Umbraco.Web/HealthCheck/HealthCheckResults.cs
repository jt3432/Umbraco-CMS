﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MarkdownSharp;
using Umbraco.Core.Configuration.HealthChecks;
using Umbraco.Core.Logging;

namespace Umbraco.Web.HealthCheck
{
    internal class HealthCheckResults
    {
        private readonly Dictionary<string, IEnumerable<HealthCheckStatus>> _results;
        internal readonly bool AllChecksSuccessful;

        internal HealthCheckResults(IEnumerable<HealthCheck> checks)
        {
            _results = checks.ToDictionary(
                t => t.Name, 
                t => {
                    try
                    {
                        return t.GetStatus();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error<HealthCheckResults>(string.Format("Error running scheduled health check: {0}", t.Name), ex);
                        var message = string.Format("Health check failed with exception: {0}. See logs for details.", ex.Message);
                        return new List<HealthCheckStatus>
                        {
                            new HealthCheckStatus(message)
                            {
                                ResultType = StatusResultType.Error
                            }
                        };
                    }                    
                });
                
            // find out if all checks pass or not
            AllChecksSuccessful = true;
            foreach (var result in _results)
            {
                var checkIsSuccess = result.Value.All(x => x.ResultType == StatusResultType.Success);
                if (checkIsSuccess == false)
                {
                    AllChecksSuccessful = false;
                    break;
                }
            }
        }

        internal void LogResults()
        {
            LogHelper.Info<HealthCheckResults>("Scheduled health check results:");
            foreach (var result in _results)
            {
                var checkName = result.Key;
                var checkResults = result.Value;
                var checkIsSuccess = result.Value.All(x => x.ResultType == StatusResultType.Success);
                if (checkIsSuccess)
                {
                    LogHelper.Info<HealthCheckResults>(string.Format("    Checks for '{0}' all completed succesfully.", checkName));
                }
                else
                {
                    LogHelper.Warn<HealthCheckResults>(string.Format("    Checks for '{0}' completed with errors.", checkName));
                }

                foreach (var checkResult in checkResults)
                {
                    LogHelper.Info<HealthCheckResults>(string.Format("        Result: {0}, Message: '{1}'", checkResult.ResultType, checkResult.Message));
                }
            }
        }

        internal string ResultsAsMarkDown(HealthCheckNotificationVerbosity verbosity, bool slackMarkDown = false)
        {
            var newItem = "- ";
            if (slackMarkDown)
            {
                newItem = "• ";
            }

            var sb = new StringBuilder();

            foreach (var result in _results)
            {
                var checkName = result.Key;
                var checkResults = result.Value;
                var checkIsSuccess = result.Value.All(x => x.ResultType == StatusResultType.Success);

                // add a new line if not the first check
                if (result.Equals(_results.First()) == false)
                {
                    sb.Append(Environment.NewLine);
                }

                if (checkIsSuccess)
                {
                    sb.AppendFormat("{0}Checks for '{1}' all completed succesfully.{2}", newItem, checkName, Environment.NewLine);
                }
                else
                {
                    sb.AppendFormat("{0}Checks for '{1}' completed with errors.{2}", newItem, checkName, Environment.NewLine);
                }

                foreach (var checkResult in checkResults)
                {
                    sb.AppendFormat("\t{0}Result: '{1}'", newItem, checkResult.ResultType);

                    // With summary logging, only record details of warnings or errors
                    if (checkResult.ResultType != StatusResultType.Success || verbosity == HealthCheckNotificationVerbosity.Detailed)
                    {
                        sb.AppendFormat(", Message: '{0}'", SimpleHtmlToMarkDown(checkResult.Message, slackMarkDown));
                    }

                    sb.AppendLine(Environment.NewLine);
                }
            }

            return sb.ToString();
        }

        internal string ResultsAsHtml(HealthCheckNotificationVerbosity verbosity)
        {
            var mark = new Markdown();
            var html = mark.Transform(ResultsAsMarkDown(verbosity));
            html = ApplyHtmlHighlighting(html);
            return html;
        }

        private string ApplyHtmlHighlighting(string html)
        {
            html = ApplyHtmlHighlightingForStatus(html, StatusResultType.Success, "5cb85c");
            html = ApplyHtmlHighlightingForStatus(html, StatusResultType.Warning, "f0ad4e");
            return ApplyHtmlHighlightingForStatus(html, StatusResultType.Error, "d9534f");
        }

        private string ApplyHtmlHighlightingForStatus(string html, StatusResultType status, string color)
        {
            return html
                .Replace("Result: '" + status + "'", "Result: <span style=\"color: #" + color + "\">" + status + "</span>");
        }

        private string SimpleHtmlToMarkDown(string html, bool slackMarkDown = false)
        {
            if (slackMarkDown)
            {
                return html.Replace("<strong>", "*")
                    .Replace("</strong>", "*")
                    .Replace("<em>", "_")
                    .Replace("</em>", "_");
            }
            return html.Replace("<strong>", "**")
                .Replace("</strong>", "**")
                .Replace("<em>", "*")
                .Replace("</em>", "*");
        }
    }
}