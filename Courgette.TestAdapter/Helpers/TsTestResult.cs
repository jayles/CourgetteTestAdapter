using System;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

public class TsTestResult {
	public string testFilename;
	public string fqName;
	public string displayName;
	public TestOutcome testOutcome;
	public string errorMessage;
	public string errorStackTrace;
	public DateTime startTime;
	public DateTime endTime;
	public long durationMs;
}
