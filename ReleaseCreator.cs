using Bars.GitUtils;
using System.Text;
using LibGit2Sharp;

// TODO Доделать valTOvar.Add("RepositoryProj", "PRJ-ORACLE19C_P2"); Видимо это ветка, как tagTo(tagNew)
namespace ReleaseMaker
{
    class GitRepo : Methods
    {
#nullable disable
        public static void Main(string[] args)
        {
            tagOld = args[0]; //tagFrom
            tagNew = args[1]; //tagTo
            var tagFrom = tagOld;
            var tagTo = tagNew;

            releaseNo = args[2];
            targetBranch = args[3];
            dbType = args[4];
            //string tagNew = "test";

            valTOvar.Add("TargetBranch", targetBranch);

            Start();
            // Import Codepage
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Make Diff Repo
            var rh = new RepoHandler(valTOvar["RepositoryPath"], valTOvar["RepositoryUser"], valTOvar["RepositoryPass"]);

            Repository repo = new(valTOvar["RepositoryPath"]);

            ///////////////////////////////////////////////////////////////////////////////////////////////////////
            if (dbType == "ORA")
            {

                rh.CheckoutBranch(valTOvar["RepositoryProj"]);
                rh.PullBranch();

                var diffOut = rh.DiffBetweenTagsSQL(tagFrom, tagTo);

                // --------------------  SQL
                //                string itemVal;
                foreach (var diff in diffOut)
                {
                    string itemVal = diff.ToString()[2..].ToLower().Replace("\\", "/").Replace("\\\\", "/").Replace('/', Path.DirectorySeparatorChar);

                    // File Destination
                    if (filesSQLDict.Contains(itemVal))
                        Logging($"File {itemVal} is duplicated.");
                    else
                        filesSQLDict.Add(itemVal);

                    // Directoty Destination
                    if (!schemDiffListArr.Contains(itemVal[..itemVal.IndexOf("\\")].ToUpper()))
                        schemDiffListArr.Add(itemVal[..itemVal.IndexOf("\\")].ToUpper());
                }

                // Make install.sql
                string installSql = MakeInstallSQL(valTOvar, filesSQLDict);

                // Create Files
                File.WriteAllText(valTOvar["ReleasePath"] + Path.DirectorySeparatorChar + "sql" + Path.DirectorySeparatorChar + "install.bat",
                                  CreateBat(), Encoding.GetEncoding("windows-1251"));
                File.WriteAllText(valTOvar["ReleasePath"] + Path.DirectorySeparatorChar + "sql" + Path.DirectorySeparatorChar + "bc_go.sql",
                                  CreateBcGo(), Encoding.GetEncoding("windows-1251"));
                File.WriteAllText(valTOvar["ReleasePath"] + Path.DirectorySeparatorChar + "sql" + Path.DirectorySeparatorChar + "params.sql",
                                  CreateParams(logPassDict, valTOvar["DbName"]), Encoding.GetEncoding("windows-1251"));
                File.WriteAllText(valTOvar["ReleasePath"] + Path.DirectorySeparatorChar + "sql" + Path.DirectorySeparatorChar + "install.sql",
                                  installSql, Encoding.GetEncoding("windows-1251"));

                string installStopLine = $"--------stop gather patches from {tagOld} to {tagNew} for {valTOvar["ReleaseName"]} -------";

                string[] gitRepoInstallFile = File.ReadAllLines(valTOvar["RepositoryPath"] + Path.DirectorySeparatorChar + "sql" + Path.DirectorySeparatorChar + "install.sql", Encoding.UTF8);

                if (installStopLine != gitRepoInstallFile[^1])
                    File.AppendAllText(valTOvar["RepositoryPath"] + Path.DirectorySeparatorChar + "sql" + Path.DirectorySeparatorChar + "install.sql", "\n" + installStopLine, Encoding.UTF8);

                // --------------------  WEB
                installStopLine = $"{releaseNo}({tagOld}-{tagNew})";

                fileToRepoAdd = "web" + Path.DirectorySeparatorChar + "barsroot" + Path.DirectorySeparatorChar + "version.abs";
                File.AppendAllText(valTOvar["RepositoryPath"] + Path.DirectorySeparatorChar + fileToRepoAdd, "\n" + installStopLine);
                rh.AddFileToRepo(fileToRepoAdd);

                fileToRepoAdd = "web_rozdrib" + Path.DirectorySeparatorChar + "barsroot" + Path.DirectorySeparatorChar + "version.abs";
                File.WriteAllText(valTOvar["RepositoryPath"] + Path.DirectorySeparatorChar + fileToRepoAdd, installStopLine);
                rh.AddFileToRepo(fileToRepoAdd);

                if (repoDirectories.IndexOf("sql") != -1)
                {
                    fileToRepoAdd = "sql" + Path.DirectorySeparatorChar + "install.sql";
                    rh.AddFileToRepo(fileToRepoAdd);
                }
                try { rh.CommitRepoChanges(valTOvar["ReleaseName"]); }
                catch (Exception ex)
                {
                    Logging("Commit не выполнился.\nВозможно не найдены изменённые файлы.\n\n");
                    Logging("\nException: " + ex);
                    Logging("----------ReleaseCreator.ReleaseMaker.Methods.MakeInstallSQL()----------");
                }
                var diffSLine = rh.DiffBetweenTagsWEB(tagOld, tagNew);

                File.WriteAllText(valTOvar["ReleasePath"] + Path.DirectorySeparatorChar + "sql" + Path.DirectorySeparatorChar + "install.bat", CreateBat(), Encoding.GetEncoding("windows-1251"));
                File.Delete(valTOvar["ReleasePath"] + Path.DirectorySeparatorChar + "remove_web_files.bat");

                foreach (var diffLine in diffSLine)
                {
                    // M.A.R.D.
                    //--Added & Modified & Renamed--//
                    if (diffLine.Item3 == ChangeKind.Added || diffLine.Item3 == ChangeKind.Modified || diffLine.Item3 == ChangeKind.Renamed)
                    {
                        dstFile = Path.Combine(valTOvar["ReleasePath"], diffLine.Item1.ToLower());
                        srcFile = Path.Combine(valTOvar["RepositoryPath"], diffLine.Item1);
                        dstPath = dstFile[..dstFile.LastIndexOf(Path.DirectorySeparatorChar)];
                        // create directory if it not exists 
                        if (!Directory.Exists(dstPath))
                            Directory.CreateDirectory(dstPath.ToLower());

                        // Copy file from repository to release
                        if (File.Exists(srcFile))
                            File.Copy(srcFile, dstFile, true);
                        else
                            Logging($"File {srcFile} not exists.");
                    }
                    //--Deleted & Renamed--//
                    if (diffLine.Item3 == ChangeKind.Deleted || diffLine.Item3 == ChangeKind.Renamed)
                    {
                        if (diffLine.Item2.Length > 0)
                            File.AppendAllText(valTOvar["ReleasePath"] + Path.DirectorySeparatorChar + "remove_web_files.bat",
                                              $"del {diffLine.Item2}\n", Encoding.GetEncoding("windows-1251"));
                    }
                }
                rh.Dispose();
            }
            if (dbType == "PSQL")
            {
                string installPSQL = "";
                string curPSQLPath = "";

                string loginDbPSQL = $"chcp 1251\npsql -d postgresql://{valTOvar["Login"]}:{valTOvar["Pass"]}@{valTOvar["Host"]}:{valTOvar["Port"]}/{valTOvar["DbName"]}"
                                   + " -q -f install.psql -L install.log ON_ERROR_STOP=on";

                File.WriteAllText(valTOvar["ReleasePath"] + "\\install.bat", loginDbPSQL);

                rh.CheckoutBranch(targetBranch);
                rh.PullBranch();

                var diffSLine = rh.DiffBetweenTagsPSQL(tagOld, tagNew);

                List<string> schemas = new();
                List<string> objects = new();
                List<string> dstFiles = new();

                foreach (var diff in diffSLine)
                {
                    string item1 = diff.Item1.ToLower().Replace(" ", "");
                    string dstFile = valTOvar["ReleasePath"] + @"\" + item1;                   // destination (Release)
                    string srcFile = valTOvar["RepositoryPath"] + @"\" + diff.Item2.ToLower(); // sourse (Repository)

                    bool cond = srcFile.LastIndexOf("install.bat") != -1 && srcFile[srcFile.LastIndexOf("install.bat")..] == "install.bat" ||
                                srcFile.LastIndexOf("install.psql") != -1 && srcFile[srcFile.LastIndexOf("install.psql")..] == "install.psql";

                    if (!cond)
                    {
                        string directory = dstFile[..dstFile.LastIndexOf(Path.DirectorySeparatorChar)];
                        if (!Directory.Exists(directory))
                            Directory.CreateDirectory(directory);

                        if (!File.Exists(dstFile))
                        {
                            File.Copy(srcFile, dstFile);
                            dstFiles.Add(dstFile);

                            if (CountWord(item1, Path.DirectorySeparatorChar.ToString()) == 1)
                                schemas.Add($"{item1.Replace(@"\", "/")}\nset search_path to {item1.Replace(@"\", "/")[..item1.Replace(@"\", "/").IndexOf("/")]}" + ";");
                            else
                            {
                                int beg = item1.IndexOf(Path.DirectorySeparatorChar) + 1;
                                int end = beg + item1[beg..item1.Length].IndexOf(Path.DirectorySeparatorChar);
                                string item = $"{item1[beg..end]}\n";
                                if (!objects.Exists(element => element == item))
                                {
                                    objects.Add($"{item1[beg..end]}\n");
                                }
                            }
                        }
                        else Logging(message: $"{dstFile} file is duplicated.");
                    }
                }

                List<string> objectsSort = SortArr(objects.ToString(), valTOvar["OrdListObjPSQL"]);

                string[] orderObjects = valTOvar["OrdListObjPSQL"].Replace(" ", "").Split(",");

                // Create install.psql
                foreach (var schem in schemas)
                {
                    if (curPSQLPath != schem[..schem.IndexOf("/")])
                    {
                        curPSQLPath = schem[..schem.IndexOf("/")];
                        installPSQL += "\n\n\\i " + schem;

                    }
                    foreach (var obj in objectsSort)
                    {
                        foreach (var item in dstFiles)
                        {
                            string itemTMP = item.Replace(valTOvar["ReleasePath"], "").Replace("\\", "/");

                            if (EntryNum(schem, '/', 1) + obj + "/" == EntryNum(itemTMP[1..], '/', 2))
                                installPSQL += "\n\\i " + itemTMP[1..];
                        }
                    }
                }

                File.WriteAllText(valTOvar["ReleasePath"] + "\\install.psql", installPSQL[1..]);
                rh.Dispose();
            }

            if (dbType != "ORA" && dbType != "PSQL")
                Logging("Database type not defined.");


            Logging(message: "Operation completed.");
        }
    }
}
