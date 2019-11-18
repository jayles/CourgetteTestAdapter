# CourgetteTestAdapter
This is the Courgette Test Adapter .vsix plugin project for use with the Courgette-ts TypeScript testing framework.

This plugin allows unit tests to be run from within Visual Studio. In normal test run mode an instance of headless Chrome is started and the tests are run inside Chrome. Once completed Chrome posts the test results back to Visual Studio and the IDE is updated with the test results. Tests can also be run in Debug mode where a visible instance of Chrome is started and left running on the desktop, which allows the user to debug their unit tests.

Currently only works with Chrome, but since the project uses Puppeteer/Chrome DevTools Protocol (CDP), it will aim to have the following  browser support:
* Chome
* Chromium
* Edge 76+ (new version of Microsoft Edge based on Chromium due to be released on 15 January 2020)
* Firefox
* Opera
* Not supported (Safari). The latest version of Safari available for Windows is version 5.1.7 (from 2012), so you cannot do any meaningful testing of Safari on Windows.

The log file is written to: `c:\Users\<username>\AppData\Local\Temp\Courgette\courgette.log`

## Issues/Limitations/To Do List
- [ ] Code has hardcoded refs to start chrome, powershell and iisexpress. These need to be put in XML config file (.runsettings)
- [ ] Need to check all runtime dependencies are present on a clean VM (both Windows 7 and Windows 10), as desktop PC already has VSIX SDK installed and this may be a runtime dependency
- [ ] Only tested on VS2017, not tested on VS2019 (manifest allows installation on VS2019)
- [ ] File Watchers need to be added to detect when user has edited/added files so list of tests can be updated
- [ ] Currently uses iisexpress.exe to serve files, this should be replaced with Kestrel
- [ ] When user asks to debug one or more unit tests, breakpoints should be added to Chrome so that execution pauses at entry point of each test
- [ ] Needs to be signed and packaged for Nuget
- [ ] Code currently runs all test scripts in an asynchronous fashion in separate instances of Chrome, but it would probably be faster to use a single Chrome instance as it takes some time to start and stop the Chrome process
