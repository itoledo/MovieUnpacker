using Microsoft.Extensions.Configuration;
using Serilog;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MovieUnpacker.Net
{
    class Program
    {
        public static IConfiguration Configuration { get; set; }

        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");
            Configuration = builder.Build();

            var outputFolder = Configuration["outputFolder"];

            Log.Information("args: {0}", args);

            var target = args[0];

            var atrs = File.GetAttributes(target);

            if (atrs == FileAttributes.Directory)
            {
                Log.Information("es un directorio");
                var files = Directory.EnumerateFiles(target).OrderBy(f => f).ToList();
                if (files.Any(f => f.EndsWith(".rar")))
                {
                    var rar = files.FirstOrDefault(f => f.EndsWith(".rar"));
                    Log.Information("encontramos un rar: {0}", rar);
                    var rars = new List<string>();
                    rars.Add(rar);
                    foreach (var f in files)
                    {
                        var ext = Path.GetExtension(f);
                        if ((ext.Length == 4 && (ext[0] == '.' && ext[1] == 'r' || ext[1] == 'R') && char.IsDigit(ext[2]) && char.IsDigit(ext[3])))
                        {
                            Log.Information("añadiendo archivo {0}", f);
                            rars.Add(f);
                        }
                        //                        || ext.ToLower().Equals(".rar"))
                    }
                    Descomprimir(rar, rars, outputFolder);

                    // veamos si hay una carpeta Subs
                    var subdir = Path.Combine(target, "Subs");
                    if (Directory.Exists(subdir))
                    {
                        files = Directory.EnumerateFiles(subdir).ToList();
                        if (files.Any(f => f.EndsWith(".idx")))
                        {
                            // a copiar
                            var fidx = files.First(f => f.EndsWith(".idx"));
                            var fname = Path.ChangeExtension(fidx, "sub");
                            var fsub = files.FirstOrDefault(f => f == fname);
                            if (fsub != null)
                            {
                                // tenemos idx y sub
                                Log.Information("copiando idx y sub");
                                File.Copy(fidx, Path.Combine(outputFolder, Path.GetFileName(fidx)));
                                File.Copy(fsub, Path.Combine(outputFolder, Path.GetFileName(fsub)));
                            }
                            else
                            {
                                fname = Path.ChangeExtension(fidx, "rar");
                                var frar = files.FirstOrDefault(f => f == fname);
                                if (frar != null)
                                {
                                    // tenemos idx y sub
                                    Log.Information("copiando idx y rar");
                                    File.Copy(fidx, Path.Combine(outputFolder, Path.GetFileName(fidx)));
                                    //                                    File.Copy(frar, Path.Combine(outputFolder, Path.GetFileName(frar)));
                                    Descomprimir(frar, new List<string>(new string[] { frar }), outputFolder);
                                }
                            }
                        }
                        else if (files.Any(f => f.EndsWith(".rar")))
                        {
                            Log.Information("descomprimiendo rar");
                            var frar = files.FirstOrDefault(f => f.EndsWith(".rar"));
                            Descomprimir(frar, new List<string>(new string[] { frar }), outputFolder);
                        }
                    }
                }
                else if (files.Any(f => f.EndsWith(".mkv") && !f.ToLower().Contains("sample")))
                {
                    var fmkv = files.First(f => f.EndsWith(".mkv") && !f.ToLower().Contains("sample"));
                    Log.Information("encontramos un mkv: {0}", fmkv);
                    Log.Information("symlink: {0}", Bash($"ln -s \"{fmkv}\" \"{outputFolder}\""));
                }
            }
            else
            {
                if (target.EndsWith(".mkv"))
                {
                    Log.Information("target es un mkv");
                    Log.Information("symlink: {0}", Bash($"ln -s \"{target}\" \"{outputFolder}\""));
                }
            }
        }

        public static string Bash(string cmd)
        {
            var escapedArgs = cmd.Replace("\"", "\\\"");

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{escapedArgs}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }

        public static void Descomprimir(string target, List<string> targets, string outputFolder)
        {
            Log.Information("descomprimiendo {0}", target);

            var streams = targets.Select(s => Path.Combine("", s)).Select(File.OpenRead).ToList(); // Stream stream = File.OpenRead(target))
            {
                using (var reader = RarArchive.Open(streams))
                {
                    foreach (var entry in reader.Entries.Where(entry => !entry.IsDirectory && entry.IsComplete))
                    {
                        Console.WriteLine(entry.Key);
                        entry.WriteToDirectory(outputFolder, new ExtractionOptions() { ExtractFullPath = false, Overwrite = true });
                        if (entry.Key.EndsWith(".rar"))
                        {
                            Log.Information("descomprimiendo recursivamente {0}", entry.Key);
                            var tmpf = Path.Combine(outputFolder, entry.Key);
                            Descomprimir(tmpf, new List<string>() { tmpf }, outputFolder);
                            try
                            {
                                File.Delete(tmpf);
                            } catch (Exception e)
                            {
                                Log.Error(e, "excepcion al borrar {0}", tmpf);
                            }
                        }
                    }
                }
            }
        }
    }
}
