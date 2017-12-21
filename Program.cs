using System;
using System.Configuration;
using System.Linq;
using System.Threading;
using Riemann;
using System.Collections.Generic;
using System.Collections;

namespace RiemannHealth {
	public class Program {
		public static void Main(string[] args) {
			string hostname;
			ushort port = 5555;
            bool useTcp = true;
			double interval = 1.0;
			ushort ttl = 5;
            bool enableCpu = true;
            bool enableLoad = true;
            bool enableMemory = true;
            bool enableDisk = true;
            bool enableNetwork = false;
			bool enableGCStats = false;
            List<string> tags = null;
            Dictionary<string, string> attributes = null;

            switch (args.Length) {
				case 0:
                    // get app configuration
					var appSettings = ConfigurationManager.AppSettings;
					hostname = appSettings["RiemannHost"];
					port = UInt16.Parse(appSettings["RiemannPort"]);
                    useTcp = Boolean.Parse(appSettings["UseTcp"]);
                    interval = (float)UInt16.Parse(appSettings["Interval"]);
					ttl = UInt16.Parse(appSettings["TTL"]);
                    enableCpu = Boolean.Parse(appSettings["EnableCpu"]);
                    enableLoad = Boolean.Parse(appSettings["EnableLoad"]);
                    enableMemory = Boolean.Parse(appSettings["EnableMemory"]);
                    enableDisk = Boolean.Parse(appSettings["EnableDisk"]);
                    enableNetwork = Boolean.Parse(appSettings["EnableNetwork"]);
                    enableGCStats = Boolean.Parse(appSettings["EnableGCstats"]);
                    string[] tagArr = appSettings["Tags"].Split(',').Select(s => s.Trim()).ToArray();
                    if (tagArr.Length > 0)
                    {
                        tags = new List<string>(tagArr);
                    }
                    // get extra attributes
                    var attributeSection = (Hashtable)ConfigurationManager.GetSection("attributes");
                    attributes = attributeSection.Cast<DictionaryEntry>().ToDictionary(d => (string)d.Key, d => (string)d.Value);
                    break;
				case 1:
					hostname = args[0];
					break;
				case 2:
					hostname = args[0];
					if (!ushort.TryParse(args[1], out port)) {
						Usage();
						Environment.Exit(-1);
					}
					break;
				default:
					Usage();
					Environment.Exit(-1);
					return;
			}

            var serviceConfig = ConfigurationManager.OpenExeConfiguration( ConfigurationUserLevel.PerUserRoamingAndLocal);
            var serviceSection = serviceConfig.GetSection("services") as ServiceInfoSection;
         
            var client = new Client(hostname, port, true, useTcp);
			var reporters = Health.Reporters(enableCpu, enableLoad, enableMemory, enableDisk, enableNetwork, enableGCStats, serviceSection.Services)
				.ToList();
			while (true) {
				foreach (var reporter in reporters) {
					string description;
					float value;

					if (reporter.TryGetValue(out description, out value)) {
						string state;
						if (value >= reporter.CriticalThreshold) {
							state = "critical";
						} else if (value >= reporter.WarnThreshold) {
							state = "warning";
						} else {
							state = "ok";
						}
						client.SendEvent(reporter.Name, state, description, value, ttl, tags, attributes);
					}
				}
				Thread.Sleep(TimeSpan.FromSeconds(interval));
			}
		}


		private static void Usage() {
			Console.WriteLine(@"riemann [[riemann-host] [riemann-port]]
If not including the host and port, please modify the App.config to suit your needs.");
		}
	}
}
