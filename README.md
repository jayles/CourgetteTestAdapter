# CourgetteTestAdapter
This is the Courgette Test Adapter .vsix plugin project for use with the Courgette-ts TypeScrtip testing framework.

This plugin allows unit tests to be run from within Visual Studio. In normal test run mode, and instance of headless Chrome is started and the tests are run inside Chrome. Once completed Chrome posts the test results back to Visual Studio and the IDE is updated with the test results. Tests can also be run in Debug mode where a visible instance of Chrome is started and left running on the desktop, which allows the user to debug their unit tests.

## Issues/Limitations/To Do List
* Needs to be signed and packaged for Nuget
* Code currently runs all test scripts in an asynchronous fashion in separate instances of Chrome, but it would probably be faster to use a single Chrome instance as it takes some time to start and stop the Chrome process
* Only tested on VS2017, not tested on VS2019 (manifest allows installation on VS2019)
* Need to check all runtime dependencies are present on a clean VM (both Windows 7 and Windows 10), as desktop PC already VSIX SDK installed and this may be a runtime dependency
