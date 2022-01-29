# UniGrader
Educational Github-based Grading System which leverages docker technology

When you run the UniGrader application it shall generate a `report.json` file located in the `SubmissionData` directory. You can upload this file at [UniGrader Dashboard](https://jbraunsmajr.github.io/UniGrader/) to view the statistics.


## Requirements
This framework was designed to run on a system which has docker installed! Also leverages the C# `Net6.0` runtime.

If you plan on contributing to this library you will need the `Net6.0` SDK installed.


## Notes
For now, must run the framework as a console application on the host machine. There is more work to be done before docker deployment is ready.

## Current Grading Types
- [Question / Answer](UniGrader/docs/qa.md)

## appsettings.json
Contains information on how the framework shall run 

| Key              | Description                                                                                                     |
|------------------|-----------------------------------------------------------------------------------------------------------------|
| type             | Type of framework to utilize                                                                                    |
| image            | The base docker image to utilize for each submission                                                            |
| outputExtensions | Optional key, an array of file extensions that will be pulled - if found - in the output directory              |
| args             | Arguments that will appear as the docker entrypoint. The first argument is automatically assumed to be `python` |

```json
{
  "PlatformConfig": {
    "Type": "QuestionAnswer",
    "BaseSubmissionImage": "base_image_for_testing",
    "OutputExtensions": [ "md", "pdf"],
    "EntryPointArgs": [
      "python", "%ENTRYPOINT_FILE%"
    ]
  }
}
```

## submissions.csv
Technically any file with a CSV extension will be grabbed.

This file must follow the two column structure of

| Column | Description                              | 
|--------|------------------------------------------|
| 1      | Unique value for identification purposes |
| 2      | github url                               |
