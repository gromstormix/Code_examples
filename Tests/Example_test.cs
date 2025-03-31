using Autofac;
using ExpServices.Core.ExpConfigService;
using ExpServices.Core.ExpLoggerService;
using LogLevel = ExpLogger.LogLevel;

namespace UnitTests.ExpConfigServiceTests
{
    public class ExpLoggerServiceTests : IClassFixture<CommonFixture>
    {
        private const string CONFIGURATION_PATH = @"D:\Expample\Projects\stend\Config";
        private readonly ILifetimeScope _rootScope;

        public ExpLoggerServiceTests(CommonFixture fixture)
        {
            _rootScope = fixture.AutofacContainer;
            var rootConfigSvc = _rootScope.Resolve<ExpConfigService>();
            rootConfigSvc.Initialize(_rootScope);

        }

        [Fact]
        public void ConstrainByLogLevelTest()
        {
            ILifetimeScope childScope = _rootScope.BeginLifetimeScope();
            ExpLoggerService childScopeLoggerSvc = childScope.Resolve<ExpLoggerService>();
            childScopeLoggerSvc.Initialize(childScope);

            childScopeLoggerSvc.LoggerSettings.LogLevel = (int)LogLevel.Error;
            childScopeLoggerSvc.LoggerSettings.LogFileName = "levels.log";

            childScopeLoggerSvc.Start(Array.Empty<string>());


            //передаем LogLevel ниже заданного минимального
            childScopeLoggerSvc.WriteToLog(LogLevel.Info, "I am not");

            //передаем LogLevel выше заданного минимального
            childScopeLoggerSvc.WriteToLog(LogLevel.Critical, "I am here");

            var files = Directory.GetFiles(childScopeLoggerSvc.LoggerSettings.LogFolderName!);
            var fileNames = files
                .Where(x => x.Contains(childScopeLoggerSvc.LoggerSettings.LogFileName.Replace(".log", ""))
                && x.Contains(DateTime.Now.Day.ToString())
                && x.Contains(DateTime.Now.Month.ToString())
                && x.Contains(DateTime.Now.Year.ToString())).ToList();

            //здесь освобождаем лог-файл, чтобы можно было прочесть его
            childScopeLoggerSvc.Stop();
            childScope.Dispose();

            var fileStreams = (fileNames.Select(fileName =>
                File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))).ToArray();

            bool isContainsFile = false;

            foreach (var stream in fileStreams)
            {
                using var reader = new StreamReader(stream);
                var textFile = reader.ReadToEndAsync().Result;
                Assert.DoesNotContain("I am not", textFile);

                if (textFile.Contains("I am here"))
                {
                    isContainsFile = true;
                }
            }

            Assert.True(isContainsFile);

        }

        [Fact]
        public void WriteToLogDefaultSettingsTest()
        {
            ILifetimeScope childScope = _rootScope.BeginLifetimeScope("child");
            ExpLoggerService childScopeLoggerSvc = childScope.Resolve<ExpLoggerService>();
            childScopeLoggerSvc.Initialize(childScope);
            childScopeLoggerSvc.Start(Array.Empty<string>());

            childScopeLoggerSvc.WriteToLog(LogLevel.Error, "WriteToLogDefaultSettingsTest");

            var files = Directory.GetFiles(childScopeLoggerSvc.LoggerSettings.LogFolderName!);
            var fileNames = files
                .Where(x => x.Contains(DateTime.Now.Day.ToString()) && x.Contains(DateTime.Now.Month.ToString()) && x.Contains(DateTime.Now.Year.ToString())).ToList();

            //здесь освобождаем лог-файл, чтобы можно было прочесть его
            childScopeLoggerSvc.Stop();
            childScope.Dispose();

            var fileStreams = (fileNames.Select(fileName =>
                File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))).ToArray();
            const bool isContainsFile = false;

            foreach (var stream in fileStreams)
            {
                using var reader = new StreamReader(stream);
                var textFile = reader.ReadToEndAsync().Result;
                if (!textFile.Contains("WriteToLogDefaultSettingsTest")) continue;
                Assert.Contains("WriteToLogDefaultSettingsTest", textFile);
                return;
            }
            Assert.True(isContainsFile);
        }
    }
}