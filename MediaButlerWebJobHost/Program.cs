﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using System.IO;
using System.Threading;
using MediaButler.Common.HostWatcher;
using System.Diagnostics;

namespace MediaButlerWebJobHost
{
    // To learn more about Microsoft Azure WebJobs SDK, please see http://go.microsoft.com/fwlink/?LinkID=320976
    public class Program
    {
        private static readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private static string ButlerWorkFlowManagerHostConfigKey = "MediaButler.Workflow.ButlerWorkFlowManagerWorkerRole";
        private static MediaButler.Common.Host.ConfigurationData myConfigData;

        private static string GetConnString()
        {


            return System.Configuration.ConfigurationSettings.AppSettings["MediaButler.ConfigurationStorageConnectionString"];
        }
        private static string GetAddListenerValue()
        {
            return System.Configuration.ConfigurationSettings.AppSettings["MediaButler.AddConsoleListener"];
        }

        static void Main()
        {

            // Add a Console Trace Listener to enable logging to Azure WebJob Dashboard
            ConsoleTraceListener consoleTracer = null;
            var addListenerValue = GetAddListenerValue();
            if (addListenerValue == "true")
            {
                consoleTracer = new ConsoleTraceListener();
                consoleTracer.Name = "mainConsoleTracer";
                Trace.Listeners.Add(consoleTracer);
                Trace.TraceInformation("Console Tracer initialized");
            }

            JobHostConfiguration config = new JobHostConfiguration();
            config.StorageConnectionString = GetConnString();
            config.DashboardConnectionString = config.StorageConnectionString;
            JobHost host = new JobHost(config);
            host.CallAsync(typeof(Program).GetMethod("RunMediaButlerWorkflow"));
            host.CallAsync(typeof(Program).GetMethod("RunMediaButlerWatcher"));
            host.RunAndBlock();

            if (addListenerValue == "true")
            {
                consoleTracer.Close();
            }
        }
        private static void Setup(string ConfigurationStorageConnectionString)
        {
            string json = MediaButler.Common.Configuration.GetConfigurationValue("roleconfig", ButlerWorkFlowManagerHostConfigKey, ConfigurationStorageConnectionString);
            myConfigData = Newtonsoft.Json.JsonConvert.DeserializeObject<MediaButler.Common.Host.ConfigurationData>(json);
            myConfigData.poisonQueue = MediaButler.Common.Configuration.ButlerFailedQueue;
            myConfigData.inWorkQueueName = MediaButler.Common.Configuration.ButlerSendQueue;
            myConfigData.ProcessConfigConn = ConfigurationStorageConnectionString;
            myConfigData.MaxCurrentProcess = myConfigData.MaxCurrentProcess;
            myConfigData.SleepDelay = myConfigData.SleepDelay;
            myConfigData.MaxDequeueCount = myConfigData.MaxDequeueCount;

        }
        [NoAutomaticTrigger]
        public static async Task RunMediaButlerWorkflow()
        {

            Setup(GetConnString());
            MediaButler.Common.Host.MediaButlerHost xHost = new MediaButler.Common.Host.MediaButlerHost(myConfigData);

            await xHost.ExecuteAsync(cancellationTokenSource.Token);


        }

        [NoAutomaticTrigger]
        public static async Task RunMediaButlerWatcher()
        {
            MediaButlerWatcherHost XHost = new MediaButlerWatcherHost(GetConnString());
            await XHost.Run();

        }
    }
}
