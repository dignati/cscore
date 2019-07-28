using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using com.csutil.io.db;
using LiteDB;
using Xunit;

namespace com.csutil.tests.io.db {

    public class LiteDbPerformanceTest1 {

        public LiteDbPerformanceTest1(Xunit.Abstractions.ITestOutputHelper logger) { logger.UseAsLoggingOutput(); }

        [Fact]
        async void PerformanceTest1() {
            AssertV2.throwExeptionIfAssertionFails = true;

            var dataTree = NewTreeLayer("1", 1000, () => NewTreeLayer("2", 2, () => NewTreeLayer("3", 4, () => NewTreeLayer("4", 1))));

            var testFolder = EnvironmentV2.instance.GetOrAddTempFolder("tests.io.db").CreateV2();
            Log.d("Path=" + testFolder);
            var dbFile = testFolder.GetChild("PerformanceTestDB_" + Guid.NewGuid().ToString());

            // Open database (or create if doesn't exist)
            using (var db = new LiteDatabase(dbFile.FullPath())) {
                await InsertIntoDb(dataTree, db);
                await ReadFromDb(dataTree, db);
            }

            await WriteFiles(dataTree, testFolder);
            await ReadFiles(dataTree, testFolder);

            // cleanup after the test
            await ParallelExec(dataTree, (elem) => { GetFileForElem(testFolder, elem).DeleteV2(); });
            Assert.True(dbFile.DeleteV2());
            Assert.False(dbFile.ExistsV2());
        }

        private static async Task ReadFromDb(List<TreeElem> dataTree, LiteDatabase db) {
            var readTimer = Log.MethodEntered();
            var elements = db.GetCollection<TreeElem>("elements");
            await ParallelExec(dataTree, (elem) => {
                var found = elements.FindById(elem.id);
                Assert.Equal(elem.name, found.name);
            });
            Log.MethodDone(readTimer, 200);
        }

        private static async Task InsertIntoDb(List<TreeElem> dataTree, LiteDatabase db) {
            var insertTimer = Log.MethodEntered();
            var elements = db.GetCollection<TreeElem>("elements");
            await ParallelExec(dataTree, (elem) => {
                elements.Insert(elem);
            });
            Log.MethodDone(insertTimer, 700);
        }

        private static Task ParallelExec<T>(IEnumerable<T> data, Action<T> actionPerElement) {
            return Task.WhenAll(data.Map(elem => Task.Run(() => actionPerElement(elem))));
        }

        private static async Task WriteFiles(List<TreeElem> dataTree, DirectoryInfo testFolder) {
            var insertTimer = Log.MethodEntered();
            await ParallelExec(dataTree, (elem) => {
                GetFileForElem(testFolder, elem).SaveAsJson(elem);
            });
            Log.MethodDone(insertTimer, 600);
        }

        private static async Task ReadFiles(List<TreeElem> dataTree, DirectoryInfo testFolder) {
            var readTimer = Log.MethodEntered();
            var reader = JsonReader.GetReader();
            await ParallelExec(dataTree, (elem) => {
                var found = GetFileForElem(testFolder, elem).LoadAs<TreeElem>();
                Assert.Equal(elem.name, found.name);
            });
            Log.MethodDone(readTimer, 1000);
        }

        private static FileInfo GetFileForElem(DirectoryInfo testFolder, TreeElem elem) {
            Assert.False(elem.id.IsNullOrEmpty());
            return testFolder.GetChild(elem.id + ".elem");
        }

        private class TreeElem : HasId {
            public string id { get; set; }
            public string name { get; set; }
            public List<TreeElem> children { get; set; }
            public string GetId() { return id; }
        }

        private List<TreeElem> NewTreeLayer(string layerName, int nodeCount, Func<List<TreeElem>> CreateChildren = null) {
            var l = new List<TreeElem>();
            for (int i = 1; i <= nodeCount; i++) { l.Add(NewTreeElem("Layer " + layerName + " - Node " + i, CreateChildren)); }
            return l;
        }

        private TreeElem NewTreeElem(string nodeName, Func<List<TreeElem>> CreateChildren = null) {
            return new TreeElem { id = Guid.NewGuid().ToString(), name = nodeName, children = CreateChildren?.Invoke() };
        }
    }

}