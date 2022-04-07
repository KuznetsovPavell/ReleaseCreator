using Newtonsoft.Json;
using System.Text.RegularExpressions;

//+EC Общий функционал и переменные.
//+EC Если нет пароля

namespace ReleaseMaker
{
#nullable disable
    public class Methods
    {
        internal static void Start()
        {
            ValToVar();
            CreateReleaseDir();
            Logging(message: "-=Start=-"); // Logging process.
        }
        internal static void CreateReleaseDir() // Make main release dir
        {
            if (Directory.Exists(valTOvar["ReleasePath"].ToLower()))
                Directory.Delete(valTOvar["ReleasePath"].ToLower(), true);
            Directory.CreateDirectory(valTOvar["ReleasePath"].ToLower());
        }

        //+EC Variables
        protected readonly static Dictionary<string, string> logPassDict = new();
        protected readonly static List<string> repoDirectories = new();

        internal static string srcFile;       // файл источника(Репозитория) (+ каталог \sql\)
        internal static string dstFile;       // файл назначения(Релиза)
        internal static string dstPath;       // калалог назначения(Релиза)
        internal static string fileToRepoAdd; // Служебная переменная. Содержит путь + имя файла web*\...\version.abs и sql*\...\install.bat в локальном репозитории

        internal static List<string> schemDiffListArr = new(); // перечень схем, где были изменения
        internal static List<string> filesSQLDict = new(); // File

        internal static string targetBranch;
        internal static string dbType;
        internal static string releaseNo;
        internal static ModelJson parJSON;
        internal static string tagOld;
        internal static string tagNew;
        internal static string projectName;

        internal static Dictionary<string, string> valTOvar = new(); //+EC Словарь основных параметров

        internal static void Logging(string message)
        {
            Console.WriteLine(message);

            if (File.Exists(valTOvar["LogFile"]))
                File.AppendAllText(valTOvar["LogFile"], $"{DateTime.Now:G} {message}\n");
            else
                File.WriteAllText(valTOvar["LogFile"], $"{DateTime.Now:G} {message}\n\n");
        }

        public static void ValToVar()
        {
            parJSON = MakeParamsFromJSON();
            RepoPrms();

            string schemsList = "";
            foreach (var proj in parJSON.Project)
            {
                if (proj.ProjectName == projectName)
                {
                    if (proj.Directory != null)
                    {
                        foreach (var dir in proj.Directory)
                            repoDirectories.Add(dir);
                    }
                    if (proj.SchemsList != null && dbType == "ORA")
                    {
                        foreach (var schem in proj.SchemsList)
                        {
                            schemsList += $"'{schem.Schem}', ";
                            try { logPassDict.Add(schem.Schem, schem.Pass); }
                            catch
                            {
                                Logging($"{schem.Schem} schem is duplicated.");
                                Logging("----------Bars.GitUtils.RepoHandler.cs.ValToVar()----------");
                            }
                        }
                    }
                }
            }
            if (dbType == "ORA")
            {
                valTOvar.Add("SchemsList", schemsList.Remove(schemsList.Length - 2));
            }
        }
        internal static void RepoPrms()
        {
            valTOvar.Add("RepositoryPath", parJSON.GitRepoDir); //+EC RepositoryPath
            foreach (var conn in parJSON.Dbconnect)             //+EC ReleasePath, DbName,...   
            {
                if (conn.TargetBranch == targetBranch)
                {
                    if (conn.ReleaseName != null)
                    {
                        valTOvar.Add("ReleasePath", parJSON.ReleasesDir + Path.DirectorySeparatorChar + conn.ReleaseName + '_' + releaseNo);
                        valTOvar.Add("ReleaseName", conn.ReleaseName + '_' + releaseNo);
                        valTOvar.Add("LogFile", valTOvar["ReleasePath"] + "\\" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".log");
                    }
                    if (conn.Oracle != null && dbType == "ORA")
                        valTOvar.Add("DbName", conn.Oracle.DbName);
                    if (conn.Postgres != null && dbType == "PSQL")
                    {
                        valTOvar.Add("DbName", conn.Postgres.DbName);
                        valTOvar.Add("Host", conn.Postgres.Host);
                        valTOvar.Add("Port", conn.Postgres.Port);
                        valTOvar.Add("Login", conn.Postgres.Login);
                        valTOvar.Add("Pass", conn.Postgres.Pass);
                    }
                }
            }

            valTOvar.Add("RepositoryUser", "pavlo.kuznetsov");
            valTOvar.Add("RepositoryPass", "+Zse4M55(");
            valTOvar.Add("JiraURL", parJSON.JiraURL);
            valTOvar.Add("JiraLogin", parJSON.JiraLogin);
            valTOvar.Add("JiraPass", parJSON.JiraPass);

            if (dbType == "PSQL")
                valTOvar.Add("OrdListObjPSQL", parJSON.OrdListObjPSQL);
        }

        internal static string SchemSwitch(string paramSchem)
        {
            string lineOut = "\nprompt ...\nprompt ... loading params\nprompt ...\n@params.sql";
            lineOut += "\nwhenever sqlerror exit\nprompt ...\nprompt ... connecting as ";
            lineOut += paramSchem.ToLower();

            lineOut += "\nconn " + paramSchem.ToLower() + "@&&dbname/&&" + paramSchem.ToLower() + "_pass";
            if (paramSchem.ToUpper() == "SYS")
                lineOut += " as sysdba";
            lineOut += "\nwhenever sqlerror continue\n\n";
            return lineOut;
        }
        internal static string CreateBat()
        {
            return string.Format("chcp 1251\n"
                                   + "set NLS_LANG=AMERICAN_AMERICA.CL8MSWIN1251\n"
                                   + "mkdir log\n"
                                   + "sqlplus /nolog @install.sql\n"
                                   + "\n"
                                   + "echo off\n"
                                   + "set sword=dbname\n"
                                   + "set file=params.sql\n"
                                   + "for /f \"delims=\" %%a in ('find \"%sword%\" %file%') do set db=%%a\n"
                                   + "set db=%db:define dbname=%\n"
                                   + "set db=%db:~1%\n"
                                   + "start log{0}install_(\"%db%\").log", Path.DirectorySeparatorChar);
        }
        internal static string CreateBcGo()
        {
            return "begin\nbars.bc.go('/');\nexception when others then\nif sqlcode in (100, -01403, -04068, -04061, -04065, -06508)"
                + " then null; else raise; end if;\nend;\n/";
        }
        internal static string SelectInvalids(string sl) //+EC Make selectInvalids
        {
            return "select owner, object_name, object_type \nfrom all_objects a where a.status = 'INVALID' and a.owner in (" + sl + ")\norder by owner, object_type;\n\n";
        }
        internal static string SelectErrors(string sl) //+EC Make selectErrors
        {
            return "select owner, type, name, line, position, text, sequence \nfrom all_errors\nwhere owner in (" + sl + ")\norder by  owner, type, name;\n\n";
        }
        internal static string CreateParams(Dictionary<string, string> paramInDict, string parInDbName)
        {
            string valOut = $"set define on\n\n-- Синонім бази даних\ndefine dbname={parInDbName}\n";

            foreach (var item in paramInDict)
            {
                valOut += $"-- Пароль користувача {item.Key.ToLower()}\n";
                if (item.Value != null)
                    valOut += $"define {item.Key.ToLower()}_pass={item.Value}\n\n";
                else
                    valOut += $"define {item.Key.ToLower()}_pass={item.Key.ToLower()}\n\n";
            }
            return valOut;
        }
        internal static ModelJson MakeParamsFromJSON()
        {
            //+EC Read JSON and Make Params from JSON
            var path = Path.Combine(Environment.CurrentDirectory, "params.json");
            var json = File.ReadAllText(path);

            return JsonConvert.DeserializeObject<ModelJson>(json);
        }
        internal static string MakeInstallSQL(Dictionary<string, string> vTov, List<string> filesSQLDict)
        {
            string selectInvalids = SelectInvalids(vTov["SchemsList"]);
            string selectErrors = SelectErrors(vTov["SchemsList"]);

            //+EC Head
            string installSql = "@params.sql\n\nset verify off\nset echo off\nset serveroutput on size 1000000";
            installSql += $"\nspool log{Path.DirectorySeparatorChar}install_(&&dbname).log\nset lines 3000\nset SQLBL on\nset timing on";
            installSql += $"\n\n\ndefine releaseName={vTov["ReleaseName"]}\n\n\n";
            installSql += SchemSwitch("bars");
            installSql += "\n\nprompt ...\nprompt ... invalid objects before install";
            installSql += "\nprompt ...\n\n" + selectInvalids + "prompt ...\nprompt ... calculating checksum for bars objects before install";
            installSql += "\nprompt ...\nexec bars.bars_release_mgr.install_begin('&&release_name');\n\n";

            //+EC Body
            string currentSqlSchem = "";
            string[] lineItems;
            string srcFile;
            string dstFile;
            string dstPath;

            foreach (var item in filesSQLDict)
            {
                srcFile = vTov["RepositoryPath"] + Path.DirectorySeparatorChar + "sql" + Path.DirectorySeparatorChar + item;
                dstFile = vTov["ReleasePath"] + Path.DirectorySeparatorChar + "sql" + Path.DirectorySeparatorChar + item.ToLower();
                dstPath = dstFile[..dstFile.LastIndexOf(Path.DirectorySeparatorChar)];

                if (!Directory.Exists(dstPath))
                    Directory.CreateDirectory(dstPath.ToLower());
                try
                {
                    File.Copy(srcFile, dstFile, true);
                    lineItems = item.Split(Path.DirectorySeparatorChar);

                    if (currentSqlSchem.ToUpper() != lineItems[0].ToUpper())
                    {
                        installSql += Methods.SchemSwitch(lineItems[0].ToLower());
                        currentSqlSchem = lineItems[0].ToUpper();
                    }
                    installSql += "\nprompt @" + item.ToLower().Trim() + "\nset define off";
                    installSql += "\n@bc_go.sql";
                    installSql += "\n@" + item.ToLower().Trim() + "\n";

                    if (lineItems[1].ToUpper() == "PACKAGE")
                        installSql += "show err\n";
                }
                catch
                {
                    if (!Directory.Exists(dstPath))
                        Logging($"\nDirectory {dstFile} not exists.");
                    if (!File.Exists(srcFile))
                        Logging($"\nFile {srcFile} not exists.");
                    Logging("----------ReleaseCreator.ReleaseMaker.Methods.MakeInstallSQL()----------");
                }
            }
            installSql += Methods.SchemSwitch("sys");
            installSql += "prompt ...\nprompt ... compiling schemas\nprompt ...\n\n";

            foreach (var item in logPassDict)
            {
                installSql += $"prompt  >> schema {item.Key}\n\nEXECUTE sys.UTL_RECOMP.RECOMP_SERIAL('{item.Key}');\n\n";
            }

            //+EC Botom
            installSql += "\n\nprompt ...\nprompt ... calculating checksum for bars objects after install\nprompt ... \n\n";
            installSql += "exec bars.bars_release_mgr.install_end('&&releaseName');\n\nprompt ...\n";
            installSql += $"prompt ... invalid objects after install\nprompt ...\n\n{selectInvalids}\n\n";
            installSql += "prompt ...\nprompt ... errors for invalid objects\nprompt ...\n\nset line 10000\n";
            installSql += "set trimspool on\nset pagesize 10000\n\nprompt ...\n";
            installSql += $"prompt ... errors for invalid objects\nprompt ...\n\n{selectErrors}\n\n\n";
            installSql += "spool off\nquit";

            return installSql;
        }

        internal static int CountWord(string source, string search) //+EC Подсчитывает количество вхождений search в source
        {
            string pattern = $"\\b{Regex.Escape(search)}\\b";
            return new Regex(pattern, RegexOptions.IgnoreCase).Matches(source).Count;
        }

        internal static List<string> SortArr(string source, string filter) //+EC Сортировка по заданному списку
        {
            List<string> sourceList = source.Replace(" ", "").Split(",").Cast<string>().ToList();
            string[] arrFilter = filter.Replace(" ", "").Split(",");
            List<string> outList = new();
            foreach (string itemList in sourceList)
                foreach (string itemFilter in arrFilter.Where(a => a.Equals(itemList)))
                    outList.Add(itemFilter);
            foreach (var itemFilter in arrFilter)
                if (!outList.Contains(itemFilter))
                    outList.Add(itemFilter);
            return outList;
        }

        internal static string EntryNum(string pInput, char pChar, int entry) //+EC Возвращает строку (pInput) отсечённую по номеру вхождения (entry) символа (pChar)
        {
            string vInput = pInput;
            int pos = 0;
            int posFull = pos;
            int sequence = 0;
            do
            {
                posFull += pos;
                pos = vInput.IndexOf(pChar) + 1;
                vInput = vInput[pos..];
                sequence++;
            }
            while (pos > 0 && sequence <= entry);
            return pInput[0..posFull];
        }

        internal static void PrintValTOVar()
        {
            foreach (var i in valTOvar)
                Console.WriteLine($"Key: {i.Key} Value: {i.Value}");
        }
    }
}