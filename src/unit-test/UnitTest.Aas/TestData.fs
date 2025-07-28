module DsJson
let dsJson = """
{
  "RuntimeType": "System",
  "Id": 1,
  "Name": "MainSystem",
  "Guid": "88b08a1c-a0ce-4c3a-a779-71dee5e5e53e",
  "IRI": "http://example.com/ev2/system/main",
  "Author": "dualk@KWAKPC",
  "EngineVersion": "0.0",
  "LangVersion": "0.0",
  "DateTime": "2025-06-17T12:27:36",
  "Flows": [
    {
      "RuntimeType": "Flow",
      "Id": 1,
      "Name": "MainFlow",
      "Guid": "0854a365-afb1-43d8-b853-203932dc1e89",
      "Buttons": [
        {
          "RuntimeType": "Button",
          "Id": 1,
          "Name": "MyButton1",
          "Guid": "25ab316a-2d49-4365-a632-2eeaab822375"
        }
      ],
      "Lamps": [
        {
          "RuntimeType": "Lamp",
          "Id": 1,
          "Name": "MyLamp1",
          "Guid": "4531a565-1fc0-4fce-8bec-6b58292dee21"
        }
      ],
      "Conditions": [
        {
          "RuntimeType": "Condition",
          "Id": 1,
          "Name": "MyCondition1",
          "Guid": "1dd7a13f-e0e2-4eac-af62-d75ca7a20d1d"
        }
      ],
      "Actions": [
        {
          "RuntimeType": "Action",
          "Id": 1,
          "Name": "MyAction1",
          "Guid": "c0a40935-fbca-4dd6-9883-d927bf4a93a9"
        }
      ]
    }
  ],
  "Works": [
    {
      "RuntimeType": "Work",
      "Id": 1,
      "Name": "BoundedWork1",
      "Guid": "79e1fd10-12a3-4b69-9a0d-7f81185573e8",
      "Parameter": "{\"Company\":\"dualsoft\",\"Name\":\"kwak\",\"Room\":510}",
      "FlowGuid": "0854a365-afb1-43d8-b853-203932dc1e89",
      "Motion": "Fast my motion",
      "Calls": [
        {
          "RuntimeType": "Call",
          "Id": 1,
          "Name": "Call1a",
          "Guid": "c6bcc544-bf86-49f8-9d1d-b9d87fdb0354",
          "Parameter": "{\"Count\":3,\"Pi\":3.14,\"Type\":\"call\"}",
          "CallType": "Parallel",
          "ApiCalls": [
            "ac98ee1a-70e9-46c5-844f-d65c3d7a8157"
          ],
          "AutoConditions": "[\r\n  \"AutoPre 테스트 1\",\r\n  \"AutoConditions 테스트 2\"\r\n]",
          "CommonConditions": "[\r\n  \"CommonCondition1\",\r\n  \"안전조건2\"\r\n]",
          "Timeout": 30
        },
        {
          "RuntimeType": "Call",
          "Id": 2,
          "Name": "Call1b",
          "Guid": "4d785edb-d468-46d3-a549-e4d661c131b5",
          "CallType": "Repeat",
          "AutoConditions": "[]",
          "CommonConditions": "[]"
        }
      ],
      "Arrows": [
        {
          "RuntimeType": "Arrow",
          "Id": 1,
          "Guid": "6959fdc5-03bc-4275-9426-73a95683ca05",
          "Source": "c6bcc544-bf86-49f8-9d1d-b9d87fdb0354",
          "Target": "4d785edb-d468-46d3-a549-e4d661c131b5",
          "Type": "Start"
        }
      ]
    },
    {
      "RuntimeType": "Work",
      "Id": 2,
      "Name": "BoundedWork2",
      "Guid": "7ab686ee-a7c6-4b88-a979-a1f3e7daa757",
      "FlowGuid": "0854a365-afb1-43d8-b853-203932dc1e89",
      "Script": "My script",
      "Calls": [
        {
          "RuntimeType": "Call",
          "Id": 3,
          "Name": "Call2a",
          "Guid": "4a6b3937-6008-41f6-91fb-46a0f656a061",
          "AutoConditions": "[]",
          "CommonConditions": "[]"
        },
        {
          "RuntimeType": "Call",
          "Id": 4,
          "Name": "Call2b",
          "Guid": "d04d2f47-ac2d-4ed2-adc2-d9590b5e7e6f",
          "AutoConditions": "[]",
          "CommonConditions": "[]"
        }
      ],
      "Arrows": [
        {
          "RuntimeType": "Arrow",
          "Id": 2,
          "Guid": "10e24fb8-e311-4c33-a299-29ab2e658eda",
          "Source": "4a6b3937-6008-41f6-91fb-46a0f656a061",
          "Target": "d04d2f47-ac2d-4ed2-adc2-d9590b5e7e6f",
          "Type": "Reset"
        }
      ]
    },
    {
      "RuntimeType": "Work",
      "Id": 3,
      "Name": "FreeWork1",
      "Guid": "ed69568e-fb70-4987-9b7c-4882d392f382",
      "IsFinished": true
    }
  ],
  "Arrows": [
    {
      "RuntimeType": "Arrow",
      "Id": 1,
      "Name": "Work 간 연결 arrow",
      "Guid": "eefab712-daa7-4e45-afd8-79c59383ee6c",
      "Parameter": "{\"ArrowHead\":\"Diamond\",\"ArrowTail\":\"Rectangle\",\"ArrowWidth\":2.1}",
      "Source": "79e1fd10-12a3-4b69-9a0d-7f81185573e8",
      "Target": "ed69568e-fb70-4987-9b7c-4882d392f382",
      "Type": "Start"
    }
  ],
  "ApiDefs": [
    {
      "RuntimeType": "ApiDef",
      "Id": 1,
      "Name": "ApiDef1a",
      "Guid": "62663164-6c91-4768-9855-548cf3be74db",
      "IsPush": true,
      "TopicIndex": 0
    },
    {
      "RuntimeType": "ApiDef",
      "Id": 2,
      "Name": "UnusedApi",
      "Guid": "7904be5c-98c6-47e3-b922-de468b6183b8",
      "IsPush": true,
      "TopicIndex": 0
    }
  ],
  "ApiCalls": [
    {
      "RuntimeType": "ApiCall",
      "Id": 1,
      "Name": "ApiCall1a",
      "Guid": "ac98ee1a-70e9-46c5-844f-d65c3d7a8157",
      "ApiDef": "62663164-6c91-4768-9855-548cf3be74db",
      "InAddress": "InAddressX0",
      "OutAddress": "OutAddress1",
      "InSymbol": "XTag1",
      "OutSymbol": "YTag2",
      "ValueSpec": "{\r\n  \"valueType\": \"Double\",\r\n  \"value\": {\r\n    \"Case\": \"Ranges\",\r\n    \"Fields\": [\r\n      [\r\n        {\r\n          \"Lower\": null,\r\n          \"Upper\": {\r\n            \"Case\": \"Some\",\r\n            \"Fields\": [\r\n              {\r\n                \"Item1\": 3.14,\r\n                \"Item2\": {\r\n                  \"Case\": \"Open\"\r\n                }\r\n              }\r\n            ]\r\n          }\r\n        },\r\n        {\r\n          \"Lower\": {\r\n            \"Case\": \"Some\",\r\n            \"Fields\": [\r\n              {\r\n                \"Item1\": 5.0,\r\n                \"Item2\": {\r\n                  \"Case\": \"Open\"\r\n                }\r\n              }\r\n            ]\r\n          },\r\n          \"Upper\": {\r\n            \"Case\": \"Some\",\r\n            \"Fields\": [\r\n              {\r\n                \"Item1\": 6.0,\r\n                \"Item2\": {\r\n                  \"Case\": \"Open\"\r\n                }\r\n              }\r\n            ]\r\n          }\r\n        },\r\n        {\r\n          \"Lower\": {\r\n            \"Case\": \"Some\",\r\n            \"Fields\": [\r\n              {\r\n                \"Item1\": 7.1,\r\n                \"Item2\": {\r\n                  \"Case\": \"Closed\"\r\n                }\r\n              }\r\n            ]\r\n          },\r\n          \"Upper\": null\r\n        }\r\n      ]\r\n    ]\r\n  }\r\n}"
    },
    {
      "RuntimeType": "ApiCall",
      "Id": 2,
      "Name": "ApiCall1b",
      "Guid": "f38e2525-aaee-4b75-942f-e3d813414c61",
      "ApiDef": "7904be5c-98c6-47e3-b922-de468b6183b8",
      "InAddress": "X0",
      "OutAddress": "Y1",
      "InSymbol": "XTag2",
      "OutSymbol": "YTag2"
    }
  ]
}
"""


let dsProject = """
{
  "RuntimeType": "Project",
  "Id": 4,
  "Name": "MainProject",
  "Guid": "10ddd4dc-b497-419d-a8d6-d69abcf1d865",
  "Parameter": "{\"Age\":30,\"Name\":\"Alice\",\"Skills\":[\"SQL\",\"Python\"]}",
  "Database": {
    "Case": "Sqlite",
    "Fields": [
      "Data Source='F:\\Git\\dsev2\\src\\unit-test\\UnitTest.Core\\database\\..\\test-data\\[SQLite] EdObject - DsObject - OrmObject - DB insert - JSON test.sqlite3';Version=3;BusyTimeout=20000"
    ]
  },
  "Author": "dualk@KWAKPC",
  "Version": "0.0",
  "DateTime": "2025-06-17T12:27:36",
  "ActiveSystems": [
    {
      "RuntimeType": "System",
      "Id": 5,
      "Name": "MainSystem",
      "Guid": "88b08a1c-a0ce-4c3a-a779-71dee5e5e53e",
      "IRI": "http://example.com/ev2/system/main",
      "Author": "dualk@KWAKPC",
      "EngineVersion": "0.0",
      "LangVersion": "0.0",
      "DateTime": "2025-06-17T12:27:36",
      "Flows": [
        {
          "RuntimeType": "Flow",
          "Id": 5,
          "Name": "MainFlow",
          "Guid": "0854a365-afb1-43d8-b853-203932dc1e89",
          "Buttons": [
            {
              "RuntimeType": "Button",
              "Id": 5,
              "Name": "MyButton1",
              "Guid": "25ab316a-2d49-4365-a632-2eeaab822375"
            }
          ],
          "Lamps": [
            {
              "RuntimeType": "Lamp",
              "Id": 5,
              "Name": "MyLamp1",
              "Guid": "4531a565-1fc0-4fce-8bec-6b58292dee21"
            }
          ],
          "Conditions": [
            {
              "RuntimeType": "Condition",
              "Id": 5,
              "Name": "MyCondition1",
              "Guid": "1dd7a13f-e0e2-4eac-af62-d75ca7a20d1d"
            }
          ],
          "Actions": [
            {
              "RuntimeType": "Action",
              "Id": 5,
              "Name": "MyAction1",
              "Guid": "c0a40935-fbca-4dd6-9883-d927bf4a93a9"
            }
          ]
        }
      ],
      "Works": [
        {
          "RuntimeType": "Work",
          "Id": 13,
          "Name": "BoundedWork1",
          "Guid": "79e1fd10-12a3-4b69-9a0d-7f81185573e8",
          "Parameter": "{\"Company\":\"dualsoft\",\"Name\":\"kwak\",\"Room\":510}",
          "FlowGuid": "0854a365-afb1-43d8-b853-203932dc1e89",
          "Motion": "Fast my motion",
          "Calls": [
            {
              "RuntimeType": "Call",
              "Id": 17,
              "Name": "Call1a",
              "Guid": "c6bcc544-bf86-49f8-9d1d-b9d87fdb0354",
              "Parameter": "{\"Count\":3,\"Pi\":3.14,\"Type\":\"call\"}",
              "CallType": "Parallel",
              "ApiCalls": [
                "ac98ee1a-70e9-46c5-844f-d65c3d7a8157"
              ],
              "AutoConditions": "[\r\n  \"AutoPre 테스트 1\",\r\n  \"AutoConditions 테스트 2\"\r\n]",
              "CommonConditions": "[\r\n  \"안전조건1\",\r\n  \"안전조건2\"\r\n]",
              "Timeout": 30
            },
            {
              "RuntimeType": "Call",
              "Id": 18,
              "Name": "Call1b",
              "Guid": "4d785edb-d468-46d3-a549-e4d661c131b5",
              "CallType": "Repeat",
              "AutoConditions": "[]",
              "CommonConditions": "[]"
            }
          ],
          "Arrows": [
            {
              "RuntimeType": "Arrow",
              "Id": 9,
              "Guid": "6959fdc5-03bc-4275-9426-73a95683ca05",
              "Source": "c6bcc544-bf86-49f8-9d1d-b9d87fdb0354",
              "Target": "4d785edb-d468-46d3-a549-e4d661c131b5",
              "Type": "Start"
            }
          ]
        },
        {
          "RuntimeType": "Work",
          "Id": 14,
          "Name": "BoundedWork2",
          "Guid": "7ab686ee-a7c6-4b88-a979-a1f3e7daa757",
          "FlowGuid": "0854a365-afb1-43d8-b853-203932dc1e89",
          "Script": "My script",
          "Calls": [
            {
              "RuntimeType": "Call",
              "Id": 19,
              "Name": "Call2a",
              "Guid": "4a6b3937-6008-41f6-91fb-46a0f656a061",
              "AutoConditions": "[]",
              "CommonConditions": "[]"
            },
            {
              "RuntimeType": "Call",
              "Id": 20,
              "Name": "Call2b",
              "Guid": "d04d2f47-ac2d-4ed2-adc2-d9590b5e7e6f",
              "AutoConditions": "[]",
              "CommonConditions": "[]"
            }
          ],
          "Arrows": [
            {
              "RuntimeType": "Arrow",
              "Id": 10,
              "Guid": "10e24fb8-e311-4c33-a299-29ab2e658eda",
              "Source": "4a6b3937-6008-41f6-91fb-46a0f656a061",
              "Target": "d04d2f47-ac2d-4ed2-adc2-d9590b5e7e6f",
              "Type": "Reset"
            }
          ]
        },
        {
          "RuntimeType": "Work",
          "Id": 15,
          "Name": "FreeWork1",
          "Guid": "ed69568e-fb70-4987-9b7c-4882d392f382",
          "IsFinished": true
        }
      ],
      "Arrows": [
        {
          "RuntimeType": "Arrow",
          "Id": 5,
          "Name": "Work 간 연결 arrow",
          "Guid": "eefab712-daa7-4e45-afd8-79c59383ee6c",
          "Parameter": "{\"ArrowHead\":\"Diamond\",\"ArrowTail\":\"Rectangle\",\"ArrowWidth\":2.1}",
          "Source": "79e1fd10-12a3-4b69-9a0d-7f81185573e8",
          "Target": "ed69568e-fb70-4987-9b7c-4882d392f382",
          "Type": "Start"
        }
      ],
      "ApiDefs": [
        {
          "RuntimeType": "ApiDef",
          "Id": 9,
          "Name": "ApiDef1a",
          "Guid": "62663164-6c91-4768-9855-548cf3be74db",
          "IsPush": true,
          "TopicIndex": 0
        },
        {
          "RuntimeType": "ApiDef",
          "Id": 10,
          "Name": "UnusedApi",
          "Guid": "7904be5c-98c6-47e3-b922-de468b6183b8",
          "IsPush": true,
          "TopicIndex": 0
        }
      ],
      "ApiCalls": [
        {
          "RuntimeType": "ApiCall",
          "Id": 9,
          "Name": "ApiCall1a",
          "Guid": "ac98ee1a-70e9-46c5-844f-d65c3d7a8157",
          "ApiDef": "62663164-6c91-4768-9855-548cf3be74db",
          "InAddress": "InAddressX0",
          "OutAddress": "OutAddress1",
          "InSymbol": "XTag1",
          "OutSymbol": "YTag2",
          "ValueSpec": "{\r\n  \"valueType\": \"Double\",\r\n  \"value\": {\r\n    \"Case\": \"Ranges\",\r\n    \"Fields\": [\r\n      [\r\n        {\r\n          \"Lower\": null,\r\n          \"Upper\": {\r\n            \"Case\": \"Some\",\r\n            \"Fields\": [\r\n              {\r\n                \"Item1\": 3.14,\r\n                \"Item2\": {\r\n                  \"Case\": \"Open\"\r\n                }\r\n              }\r\n            ]\r\n          }\r\n        },\r\n        {\r\n          \"Lower\": {\r\n            \"Case\": \"Some\",\r\n            \"Fields\": [\r\n              {\r\n                \"Item1\": 5.0,\r\n                \"Item2\": {\r\n                  \"Case\": \"Open\"\r\n                }\r\n              }\r\n            ]\r\n          },\r\n          \"Upper\": {\r\n            \"Case\": \"Some\",\r\n            \"Fields\": [\r\n              {\r\n                \"Item1\": 6.0,\r\n                \"Item2\": {\r\n                  \"Case\": \"Open\"\r\n                }\r\n              }\r\n            ]\r\n          }\r\n        },\r\n        {\r\n          \"Lower\": {\r\n            \"Case\": \"Some\",\r\n            \"Fields\": [\r\n              {\r\n                \"Item1\": 7.1,\r\n                \"Item2\": {\r\n                  \"Case\": \"Closed\"\r\n                }\r\n              }\r\n            ]\r\n          },\r\n          \"Upper\": null\r\n        }\r\n      ]\r\n    ]\r\n  }\r\n}"
        },
        {
          "RuntimeType": "ApiCall",
          "Id": 10,
          "Name": "ApiCall1b",
          "Guid": "f38e2525-aaee-4b75-942f-e3d813414c61",
          "ApiDef": "7904be5c-98c6-47e3-b922-de468b6183b8",
          "InAddress": "X0",
          "OutAddress": "Y1",
          "InSymbol": "XTag2",
          "OutSymbol": "YTag2"
        }
      ]
    }
  ],
  "PassiveSystems": []
}
"""