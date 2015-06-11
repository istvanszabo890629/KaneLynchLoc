﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace KaneLynchLoc
{
    class KaneLynchConverter
    {
        static string version_string = "0.02";

        string file1, file2;

        public KaneLynchConverter(string[] args)
        {
            if (args.Length == 2)
            {
                file1 = args[0];
                file2 = args[1];
            }
        }

        static string GetExt(string filename)
        {
            int last_dot = filename.LastIndexOf('.');
            int last_sla = filename.LastIndexOf('\\');

            if (last_dot == -1)
                return "";

            if ((last_dot > last_sla) || last_sla == -1)
            {
                return filename.Substring(last_dot + 1);
            }

            if (last_sla == -1)
            {
                return filename;
            }

            return filename.Substring(last_sla + 1);
        }

        private void ShowInfo()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("\tKaneLynchLoc.exe src_file dst_file");
            Console.WriteLine("\t src_file Locale or language export file");
            Console.WriteLine("\t dst_file Locale or language export file");
            Console.WriteLine("\tLocale files must have the extension \".LOC\"");
            Console.WriteLine("\tLanguage export file must have the extension \".xml\"");
        }

        public bool Run()
        {
            bool valid = true;

            Console.WriteLine("KaneLynch Locale Tool v{0}", version_string);
            Console.WriteLine("Written by WRS (xentax.com)");


            valid &= (file1 != null);
            valid &= (file2 != null);

            if (valid)
            {
                valid &= File.Exists(file1);
            }

            if (!valid)
            {
                ShowInfo();
            }
            else
            {
                KaneLynchLoc loc = new KaneLynchLoc();

                bool src_is_xml = (GetExt(file1).ToLower() == "xml");
                bool dst_is_xml = (GetExt(file2).ToLower() == "xml");

                if (src_is_xml)
                {
                    valid &= loc.ReadXml(file1);
                }
                else
                {
                    valid &= loc.ReadLoc(file1);
                }

                if (!valid)
                {
                    Console.WriteLine("Error: Failed to read \"{0}\"", file1);
                }
                else
                {
                    if (dst_is_xml)
                    {
                        valid &= loc.WriteXml(file2);
                    }
                    else
                    {
                        valid &= loc.WriteLoc(file2);
                    }

                    if (valid)
                    {
                        Console.WriteLine("Success! Written out \"{0}\"", file2);
                    }
                    else
                    {
                        Console.WriteLine("Error: Failed to write \"{0}\"", file2);
                    }
                }
            }

            return valid;
        }
    }
}
