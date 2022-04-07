namespace ReleaseMaker
{
    public class ModelJson
    {

#nullable disable
        [Newtonsoft.Json.JsonProperty("jira_url")]
        public string JiraURL { get; set; }

        [Newtonsoft.Json.JsonProperty("jira_login")]
        public string JiraLogin { get; set; }

        [Newtonsoft.Json.JsonProperty("jira_pass")]
        public string JiraPass { get; set; }
        
        [Newtonsoft.Json.JsonProperty("gmail_login")]
        public string GmailLogin { get; set; }
        
        [Newtonsoft.Json.JsonProperty("gmail_pass")]
        public string GmailPass { get; set; }
        
        [Newtonsoft.Json.JsonProperty("git_repo_dir")]
        public string GitRepoDir { get; set; }
        
        [Newtonsoft.Json.JsonProperty("releases_dir")]
        public string ReleasesDir { get; set; }
        
        [Newtonsoft.Json.JsonProperty("task_file")]
        public string TaskFile { get; set; }
        
        [Newtonsoft.Json.JsonProperty("release_task_file")]
        public string ReleaseTaskFile { get; set; }
        
        [Newtonsoft.Json.JsonProperty("ord_list_obj_psql")]
        public string OrdListObjPSQL { get; set; }
        
        public DbConnect[] Dbconnect { get; set; }
        public Project[] Project { get; set; }
    }
    public class DbConnect
    {
        [Newtonsoft.Json.JsonProperty("target_branch")]
        public string TargetBranch { get; set; }
        [Newtonsoft.Json.JsonProperty("release_name")]
        public string ReleaseName { get; set; }
        public Cpostgres Postgres { get; set; }
        public Coracle Oracle { get; set; }


        public class Cpostgres
        {
            [Newtonsoft.Json.JsonProperty("db_name")]
            public string DbName { get; set; }
            public string Host { get; set; }
            public string Port { get; set; }
            public string Login { get; set; }
            public string Pass { get; set; }
        }

        public class Coracle
        {
            [Newtonsoft.Json.JsonProperty("db_name")]
            public string DbName { get; set; }
        }
    }
    public class Project
    {
        [Newtonsoft.Json.JsonProperty("project_name")]
        public string ProjectName { get; set; }
        [Newtonsoft.Json.JsonProperty("schems_list")]
        public SchemsList[] SchemsList { get; set; }
        public string[] Directory { get; set; }
    }

    public class SchemsList
    {
        public string Schem { get; set; }
        public string Pass { get; set; }
    }
}


