﻿using CaasDeploy.Library;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaasDeploy
{
    class Program
    {

        static void Main(string[] args)
        {
            Dictionary<string, string> arguments = ParseArguments(args);
            if (!ValidateArguments(arguments))
            {
                ShowUsage();
                return;
            }

            var t = Task.Run(() => PerformRequest(arguments));
            t.Wait();
        }


        private static Dictionary<string, string> ParseArguments(string[] args)
        {
            var arguments = new Dictionary<string, string>();
            for (int i=0; i<args.Length; i+=2)
            {
                if (i + 1 < args.Length)
                {
                    arguments.Add(args[i].ToLower().Substring(1), args[i + 1]);
                }
            }
            return arguments;
        }

        private static bool ValidateArguments(Dictionary<string, string> arguments)
        {
            if (!arguments.ContainsKey("action") ||
                !arguments.ContainsKey("template") ||
                !arguments.ContainsKey("parameters") ||
                !arguments.ContainsKey("region") ||
                !arguments.ContainsKey("username") ||
                !arguments.ContainsKey("password"))
            {
                return false;
            }
            return true;
        }

        private  static void ShowUsage()
        {
            Console.WriteLine("Usage: ");
            Console.WriteLine("\tCaasDeploy.exe");
            Console.WriteLine("\t\t-action Deploy|Delete");
            Console.WriteLine("\t\t-template {PathToTemplateFile}");
            Console.WriteLine("\t\t-parameters {PathToParametersFile}");
            Console.WriteLine("\t\t-region {RegionName}");
            Console.WriteLine("\t\t-username {CaaSUserName}");
            Console.WriteLine("\t\t-password {CaasPassword}");

        }

        static async Task PerformRequest(Dictionary<string, string> arguments)
        {
            var accountDetails = await CaasAuthentication.Authenticate(
                arguments["username"], arguments["password"], arguments["region"]);

            try
            {
                var d = new Deployment(arguments["template"], arguments["parameters"], 
                    arguments["region"], accountDetails);

                if (arguments["action"].ToLower() == "deploy")
                {
                    await d.Deploy();
                }
                else if (arguments["action"].ToLower() == "delete")
                {
                    await d.Delete();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
