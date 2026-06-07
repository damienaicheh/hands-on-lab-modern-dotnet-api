# KQL queries inside Application Insights

Go to Application Insights in Azure Portal, in the Monitoring section of the left-hand menu. Then click on Logs.

Select **KQL Mode** in the top right corner of the query editor.

For duplicate upload copy/paste the following query and run it:

```bash
customEvents
| where name == "Documents.Upload.Duplicate"
| order by timestamp desc
```

For message logger copy/paste the following query and run it:

```bash
traces
| where message has "Duplicate document upload rejected"
| project timestamp, severityLevel, message, CorrelationId=tostring(customDimensions.["X-Correlation-Id"]), ServiceName=tostring(customDimensions.ServiceName), operation_Id
| order by timestamp desc
```

For metrics copy/paste the following query and run it:

```bash
customMetrics
| where name == "Documents.Upload.DuplicateCount"
| summarize Total=sum(value) by bin(timestamp, 15m)
```