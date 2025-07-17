namespace T

open System
open System.Threading.Tasks
open NUnit.Framework
open FsUnit
open StackExchange.Redis
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Dual.Common.Redis.FS

[<AutoOpen>]
module RedisHashTestModule =

    [<CLIMutable>]
    type TestUser = {
        Id: int
        Name: string
        Email: string
        Age: int
        IsActive: bool
    }

    type HashTest() =
        let mutable redis: IDatabase = null
        let mutable connection: IConnectionMultiplexer = null

        [<SetUp>]
        member _.Setup() =
            try
                connection <- ConnectionMultiplexer.Connect("localhost:6379")
                redis <- connection.GetDatabase()
                redis.KeyDelete("test:user:1001") |> ignore
                redis.KeyDelete("test:user:1002") |> ignore
                redis.KeyDelete("test:product:P001") |> ignore
            with
            | ex ->
                Assert.Inconclusive($"Redis 서버에 연결할 수 없습니다: {ex.Message}")

        [<TearDown>]
        member _.TearDown() =
            if connection <> null then
                connection.Dispose()

        [<Test>]
        member _.``Redis Hash - 기본 HSET, HGET 테스트``() =
            let key = "test:user:1001"

            redis.HashSet(key, "name", "김철수") |> ignore
            redis.HashSet(key, "email", "kim@example.com") |> ignore
            redis.HashSet(key, "age", "30") |> ignore

            let name = redis.HashGet(key, "name")
            let email = redis.HashGet(key, "email")
            let age = redis.HashGet(key, "age")

            name.ToString() |> should equal "김철수"
            email.ToString() |> should equal "kim@example.com"
            age.ToString() |> should equal "30"

        [<Test>]
        member _.``Redis Hash - 복합 객체 저장 및 조회 테스트``() =
            let key = "test:user:1002"
            let user = {
                Id = 1002
                Name = "이영희"
                Email = "lee@example.com"
                Age = 25
                IsActive = true
            }

            let userJson = JsonConvert.SerializeObject(user)
            redis.HashSet(key, "data", userJson) |> ignore
            redis.HashSet(key, "id", user.Id.ToString()) |> ignore
            redis.HashSet(key, "name", user.Name) |> ignore

            let retrievedJson = redis.HashGet(key, "data")
            let retrievedUser = JsonConvert.DeserializeObject<TestUser>(retrievedJson.ToString())

            retrievedUser.Id |> should equal user.Id
            retrievedUser.Name |> should equal user.Name
            retrievedUser.Email |> should equal user.Email

        [<Test>]
        member _.``Redis Hash - HGETALL 전체 조회 테스트``() =
            let key = "test:product:P001"

            let hashFields = [|
                HashEntry("productId", "P001")
                HashEntry("name", "노트북")
                HashEntry("price", "1200000")
            |]

            redis.HashSet(key, hashFields) |> ignore
            let allFields = redis.HashGetAll(key)

            allFields.Length |> should equal 3

            let productIdField = allFields |> Array.find (fun h -> h.Name = "productId")
            productIdField.Value.ToString() |> should equal "P001"

        [<Test>]
        member _.``Redis Hash - HDEL 필드 삭제 테스트``() =
            let key = "test:user:1001"

            redis.HashSet(key, "name", "홍길동") |> ignore
            redis.HashSet(key, "email", "hong@example.com") |> ignore
            redis.HashSet(key, "city", "서울") |> ignore

            redis.HashExists(key, "name") |> should be True
            redis.HashExists(key, "city") |> should be True

            let deleted = redis.HashDelete(key, "city")
            deleted |> should be True

            redis.HashExists(key, "city") |> should be False
            redis.HashExists(key, "name") |> should be True

    type PubSubTest() =
        let mutable connection: IConnectionMultiplexer = null
        let mutable subscriber: ISubscriber = null

        [<SetUp>]
        member _.Setup() =
            try
                connection <- ConnectionMultiplexer.Connect("localhost:6379")
                subscriber <- connection.GetSubscriber()
            with
            | ex ->
                Assert.Inconclusive($"Redis 서버에 연결할 수 없습니다: {ex.Message}")

        [<TearDown>]
        member _.TearDown() =
            if connection <> null then
                connection.Dispose()

        [<Test>]
        member _.``Redis PubSub - 기본 발행/구독 테스트``() =
            let channel = "test:basic:channel"
            let mutable receivedMessage = ""
            let mutable messageReceived = false

            subscriber.SubscribeAsync(channel, fun ch msg ->
                receivedMessage <- msg.ToString()
                messageReceived <- true
            ) |> ignore

            System.Threading.Thread.Sleep(100)

            let testMessage = "Hello, Redis Pub/Sub!"
            subscriber.PublishAsync(channel, testMessage) |> ignore

            System.Threading.Thread.Sleep(300)

            messageReceived |> should be True
            receivedMessage |> should equal testMessage

            subscriber.Unsubscribe(channel)

        [<Test>]
        member _.``Redis PubSub - JSON 메시지 테스트``() =
            let channel = "test:json:channel"
            let mutable receivedUser: TestUser option = None
            let mutable messageReceived = false

            let originalUser = {
                Id = 1001
                Name = "김테스트"
                Email = "test@example.com"
                Age = 28
                IsActive = true
            }

            subscriber.SubscribeAsync(channel, fun ch msg ->
                try
                    let user = JsonConvert.DeserializeObject<TestUser>(msg.ToString())
                    receivedUser <- Some user
                    messageReceived <- true
                with ex ->
                    printfn "JSON 역직렬화 오류: %s" ex.Message
            ) |> ignore

            System.Threading.Thread.Sleep(100)

            let jsonMessage = JsonConvert.SerializeObject(originalUser)
            subscriber.PublishAsync(channel, jsonMessage) |> ignore

            System.Threading.Thread.Sleep(300)

            messageReceived |> should be True
            receivedUser |> should not' (equal None)

            let user = receivedUser.Value
            user.Id |> should equal originalUser.Id
            user.Name |> should equal originalUser.Name

            subscriber.Unsubscribe(channel)

    type IntegratedTest() =
        let mutable connection: IConnectionMultiplexer = null
        let mutable redis: IDatabase = null
        let mutable subscriber: ISubscriber = null

        [<SetUp>]
        member _.Setup() =
            try
                connection <- ConnectionMultiplexer.Connect("localhost:6379")
                redis <- connection.GetDatabase()
                subscriber <- connection.GetSubscriber()
            with
            | ex ->
                Assert.Inconclusive($"Redis 서버에 연결할 수 없습니다: {ex.Message}")

        [<TearDown>]
        member _.TearDown() =
            if connection <> null then
                connection.Dispose()

        [<Test>]
        member _.``통합 테스트 - Hash 저장 후 Pub/Sub 알림``() =
            let notifyChannel = "test:notifications"
            let hashKey = "test:user:session:1001"
            let mutable notificationReceived = false
            let mutable notificationData = ""

            subscriber.SubscribeAsync(notifyChannel, fun ch msg ->
                notificationData <- msg.ToString()
                notificationReceived <- true
            ) |> ignore

            System.Threading.Thread.Sleep(100)

            let loginTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            redis.HashSet(hashKey, "last_login", loginTime) |> ignore
            redis.HashSet(hashKey, "session_id", "sess_123456") |> ignore

            let notification = {|
                Action = "LOGIN"
                UserId = 1001
                SessionKey = hashKey
                Timestamp = loginTime
            |}
            let notificationJson = JsonConvert.SerializeObject(notification)
            subscriber.PublishAsync(notifyChannel, notificationJson) |> ignore

            System.Threading.Thread.Sleep(300)

            notificationReceived |> should be True
            notificationData |> should not' (equal "")

            let storedLoginTime = redis.HashGet(hashKey, "last_login")
            let storedSessionId = redis.HashGet(hashKey, "session_id")

            storedLoginTime.ToString() |> should equal loginTime
            storedSessionId.ToString() |> should equal "sess_123456"

            subscriber.Unsubscribe(notifyChannel)

        [<Test>]
        member _.``통합 테스트 - 여러 데이터 동기화 시뮬레이션``() =
            let syncChannel = "test:data:sync"
            let dataKey = "test:realtime:data"
            let mutable syncCount = 0

            subscriber.SubscribeAsync(syncChannel, fun ch msg ->
                syncCount <- syncCount + 1
            ) |> ignore

            System.Threading.Thread.Sleep(100)

            let operations = [|
                ("user_count", "150")
                ("active_sessions", "45")
                ("last_update", DateTime.UtcNow.ToString())
            |]

            for (field, value) in operations do
                redis.HashSet(dataKey, field, value) |> ignore
                let syncMessage = $"Updated {field} = {value}"
                subscriber.PublishAsync(syncChannel, syncMessage) |> ignore
                System.Threading.Thread.Sleep(50)

            System.Threading.Thread.Sleep(200)

            syncCount |> should equal 3

            let allData = redis.HashGetAll(dataKey)
            allData.Length |> should equal 3

            let userCount = redis.HashGet(dataKey, "user_count")
            userCount.ToString() |> should equal "150"

            subscriber.Unsubscribe(syncChannel)