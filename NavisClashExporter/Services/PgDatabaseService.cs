using System;
using System.Collections.Generic;
using Npgsql;
using NavisClashExporter.Models;

namespace NavisClashExporter.Services
{
    public class PgDatabaseService : IDisposable
    {
        private readonly string _cs;

        public PgDatabaseService(string connectionString)
        {
            _cs = connectionString;
        }

        // ── SCHEMA ─────────────────────────────────────────────────────
        public void EnsureSchema()
        {
            using (var conn = new NpgsqlConnection(_cs))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS directories (
                        id SERIAL PRIMARY KEY,
                        code TEXT NOT NULL UNIQUE,
                        created_at TIMESTAMP DEFAULT NOW());

                    CREATE TABLE IF NOT EXISTS navisworks_projects (
                        id SERIAL PRIMARY KEY,
                        name TEXT NOT NULL UNIQUE,
                        nwf_path TEXT NOT NULL DEFAULT '',
                        directory_id INTEGER REFERENCES directories(id) ON DELETE SET NULL,
                        created_at TIMESTAMP DEFAULT NOW(),
                        updated_at TIMESTAMP DEFAULT NOW());

                    CREATE TABLE IF NOT EXISTS clash_tasks (
                        id TEXT PRIMARY KEY,
                        project_id TEXT NOT NULL,
                        project_name TEXT NOT NULL,
                        nwc_folder TEXT NOT NULL,
                        status TEXT NOT NULL DEFAULT 'Pending',
                        error_message TEXT,
                        revit_version TEXT,
                        created_at TIMESTAMP DEFAULT NOW(),
                        updated_at TIMESTAMP DEFAULT NOW());

                    CREATE UNIQUE INDEX IF NOT EXISTS uix_clash_tasks_project_id
                        ON clash_tasks(project_id);

                    CREATE TABLE IF NOT EXISTS clash_tests (
                        id SERIAL PRIMARY KEY,
                        navisworks_project_id INTEGER NOT NULL REFERENCES navisworks_projects(id) ON DELETE CASCADE,
                        name TEXT NOT NULL, test_type TEXT, status TEXT,
                        tolerance DOUBLE PRECISION, left_locator TEXT, right_locator TEXT,
                        summary_total INTEGER DEFAULT 0, summary_new INTEGER DEFAULT 0,
                        summary_active INTEGER DEFAULT 0, summary_reviewed INTEGER DEFAULT 0,
                        summary_approved INTEGER DEFAULT 0, summary_resolved INTEGER DEFAULT 0,
                        updated_at TIMESTAMP DEFAULT NOW());

                    CREATE TABLE IF NOT EXISTS clash_results (
                        id SERIAL PRIMARY KEY,
                        clash_test_id INTEGER NOT NULL REFERENCES clash_tests(id) ON DELETE CASCADE,
                        guid TEXT NOT NULL, name TEXT, status TEXT,
                        distance DOUBLE PRECISION, description TEXT, grid_location TEXT,
                        point_x DOUBLE PRECISION, point_y DOUBLE PRECISION, point_z DOUBLE PRECISION,
                        created_date TIMESTAMP, image BYTEA,
                        item1_id TEXT, item1_name TEXT, item1_type TEXT, item1_layer TEXT, item1_source_file TEXT,
                        item2_id TEXT, item2_name TEXT, item2_type TEXT, item2_layer TEXT, item2_source_file TEXT);

                    CREATE TABLE IF NOT EXISTS clash_test_history (
                        id SERIAL PRIMARY KEY,
                        navisworks_project_id INTEGER NOT NULL,
                        test_name TEXT NOT NULL, record_date DATE NOT NULL,
                        summary_total INTEGER DEFAULT 0, summary_new INTEGER DEFAULT 0,
                        summary_active INTEGER DEFAULT 0, summary_reviewed INTEGER DEFAULT 0,
                        summary_approved INTEGER DEFAULT 0, summary_resolved INTEGER DEFAULT 0,
                        created_at TIMESTAMP DEFAULT NOW(),
                        UNIQUE (navisworks_project_id, test_name, record_date));
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── DIRECTORIES ────────────────────────────────────────────────
        public List<DirectoryModel> GetAllDirectories()
        {
            var list = new List<DirectoryModel>();
            using (var conn = new NpgsqlConnection(_cs))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(
                    "SELECT id,code,created_at FROM directories ORDER BY code", conn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        list.Add(new DirectoryModel
                        {
                            Id = r.GetInt32(0),
                            Code = r.GetString(1),
                            CreatedAt = r.GetDateTime(2)
                        });
                }
            }
            return list;
        }

        public DirectoryModel CreateDirectory(string code)
        {
            using (var conn = new NpgsqlConnection(_cs))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(
                    "INSERT INTO directories (code) VALUES (@c) RETURNING id, created_at", conn))
                {
                    cmd.Parameters.AddWithValue("c", code);
                    using (var r = cmd.ExecuteReader())
                    {
                        r.Read();
                        return new DirectoryModel
                        {
                            Id = r.GetInt32(0),
                            Code = code,
                            CreatedAt = r.GetDateTime(1)
                        };
                    }
                }
            }
        }

        public void UpdateDirectory(int id, string code)
        {
            using (var conn = new NpgsqlConnection(_cs))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(
                    "UPDATE directories SET code=@c WHERE id=@id", conn))
                {
                    cmd.Parameters.AddWithValue("c", code);
                    cmd.Parameters.AddWithValue("id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteDirectory(int id)
        {
            using (var conn = new NpgsqlConnection(_cs))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(
                    "DELETE FROM directories WHERE id=@id", conn))
                {
                    cmd.Parameters.AddWithValue("id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── NAVISWORKS PROJECTS ────────────────────────────────────────
        private const string ProjectSql = @"
            SELECT p.id,p.name,p.nwf_path,p.directory_id,p.created_at,p.updated_at,
                   d.id,d.code,d.created_at
            FROM navisworks_projects p
            LEFT JOIN directories d ON d.id=p.directory_id";

        private NavisworksProjectModel MapProject(NpgsqlDataReader r)
        {
            var p = new NavisworksProjectModel
            {
                Id = r.GetInt32(0),
                Name = r.GetString(1),
                NwfPath = r.GetString(2),
                DirectoryId = r.IsDBNull(3) ? (int?)null : r.GetInt32(3),
                CreatedAt = r.GetDateTime(4),
                UpdatedAt = r.GetDateTime(5)
            };
            if (!r.IsDBNull(6))
                p.Directory = new DirectoryModel
                {
                    Id = r.GetInt32(6),
                    Code = r.GetString(7),
                    CreatedAt = r.GetDateTime(8)
                };
            return p;
        }

        public List<NavisworksProjectModel> GetAllProjects()
        {
            var list = new List<NavisworksProjectModel>();
            using (var conn = new NpgsqlConnection(_cs))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(ProjectSql + " ORDER BY p.name", conn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        list.Add(MapProject(r));
                }
            }
            return list;
        }

        public NavisworksProjectModel GetProjectByName(string name)
        {
            using (var conn = new NpgsqlConnection(_cs))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(ProjectSql + " WHERE p.name=@n", conn))
                {
                    cmd.Parameters.AddWithValue("n", name);
                    using (var r = cmd.ExecuteReader())
                    {
                        return r.Read() ? MapProject(r) : null;
                    }
                }
            }
        }

        public void CreateProject(string name, string nwfPath, int? directoryId)
        {
            using (var conn = new NpgsqlConnection(_cs))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(
                    "INSERT INTO navisworks_projects (name,nwf_path,directory_id) VALUES (@n,@p,@d)", conn))
                {
                    cmd.Parameters.AddWithValue("n", name);
                    cmd.Parameters.AddWithValue("p", nwfPath);
                    cmd.Parameters.AddWithValue("d", directoryId.HasValue ? (object)directoryId.Value : DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateProject(int id, string name, string nwfPath, int? directoryId)
        {
            using (var conn = new NpgsqlConnection(_cs))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(
                    "UPDATE navisworks_projects SET name=@n,nwf_path=@p,directory_id=@d,updated_at=NOW() WHERE id=@id", conn))
                {
                    cmd.Parameters.AddWithValue("n", name);
                    cmd.Parameters.AddWithValue("p", nwfPath);
                    cmd.Parameters.AddWithValue("d", directoryId.HasValue ? (object)directoryId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteProject(int id)
        {
            using (var conn = new NpgsqlConnection(_cs))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(
                    "DELETE FROM navisworks_projects WHERE id=@id", conn))
                {
                    cmd.Parameters.AddWithValue("id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── CLASH TASKS ────────────────────────────────────────────────
        public List<ClashTaskModel> GetPendingTasks()
        {
            var list = new List<ClashTaskModel>();
            using (var conn = new NpgsqlConnection(_cs))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(
                    "SELECT id,project_id,project_name,nwc_folder,status,error_message,revit_version,created_at,updated_at " +
                    "FROM clash_tasks WHERE status='Pending' ORDER BY created_at", conn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        list.Add(new ClashTaskModel
                        {
                            Id = r.GetString(0),
                            ProjectId = r.GetString(1),
                            ProjectName = r.GetString(2),
                            NwcFolder = r.GetString(3),
                            Status = Enum.TryParse<ClashTaskStatus>(r.GetString(4), out var s)
                                ? s : ClashTaskStatus.Pending,
                            ErrorMessage = r.IsDBNull(5) ? null : r.GetString(5),
                            RevitVersion = r.IsDBNull(6) ? null : r.GetString(6),
                            CreatedAt = r.GetDateTime(7),
                            UpdatedAt = r.GetDateTime(8)
                        });
                }
            }
            return list;
        }

        public void UpdateTaskStatus(string taskId, ClashTaskStatus status, string errorMessage = null)
        {
            using (var conn = new NpgsqlConnection(_cs))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(
                    "UPDATE clash_tasks SET status=@s,error_message=@e,updated_at=NOW() WHERE id=@id", conn))
                {
                    cmd.Parameters.AddWithValue("s", status.ToString());
                    cmd.Parameters.AddWithValue("e", errorMessage != null ? (object)errorMessage : DBNull.Value);
                    cmd.Parameters.AddWithValue("id", taskId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── CLASH TESTS ────────────────────────────────────────────────
        public void DeleteClashDataForProject(int projectId)
        {
            using (var conn = new NpgsqlConnection(_cs))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(@"
                    DELETE FROM clash_results WHERE clash_test_id IN
                        (SELECT id FROM clash_tests WHERE navisworks_project_id=@pid);
                    DELETE FROM clash_tests WHERE navisworks_project_id=@pid;", conn))
                {
                    cmd.Parameters.AddWithValue("pid", projectId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public int InsertClashTest(ClashTestModel t)
        {
            using (var conn = new NpgsqlConnection(_cs))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(@"
                    INSERT INTO clash_tests
                        (navisworks_project_id,name,test_type,status,tolerance,
                         left_locator,right_locator,
                         summary_total,summary_new,summary_active,
                         summary_reviewed,summary_approved,summary_resolved)
                    VALUES (@pid,@n,@tt,@st,@tol,@ll,@rl,@tot,@new,@act,@rev,@app,@res)
                    RETURNING id", conn))
                {
                    cmd.Parameters.AddWithValue("pid", t.NavisworksProjectId);
                    cmd.Parameters.AddWithValue("n", t.Name ?? "");
                    cmd.Parameters.AddWithValue("tt", t.TestType ?? "");
                    cmd.Parameters.AddWithValue("st", t.Status ?? "");
                    cmd.Parameters.AddWithValue("tol", t.Tolerance.HasValue ? (object)t.Tolerance.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("ll", t.LeftLocator ?? "");
                    cmd.Parameters.AddWithValue("rl", t.RightLocator ?? "");
                    cmd.Parameters.AddWithValue("tot", t.SummaryTotal);
                    cmd.Parameters.AddWithValue("new", t.SummaryNew);
                    cmd.Parameters.AddWithValue("act", t.SummaryActive);
                    cmd.Parameters.AddWithValue("rev", t.SummaryReviewed);
                    cmd.Parameters.AddWithValue("app", t.SummaryApproved);
                    cmd.Parameters.AddWithValue("res", t.SummaryResolved);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        // ── CLASH RESULTS ──────────────────────────────────────────────
        public void InsertClashResults(List<ClashResultModel> results)
        {
            if (results == null || results.Count == 0) return;

            using (var conn = new NpgsqlConnection(_cs))
            {
                conn.Open();
                foreach (var res in results)
                {
                    using (var cmd = new NpgsqlCommand(@"
                        INSERT INTO clash_results
                            (clash_test_id,guid,name,status,distance,description,grid_location,
                             point_x,point_y,point_z,created_date,image,
                             item1_id,item1_name,item1_type,item1_layer,item1_source_file,
                             item2_id,item2_name,item2_type,item2_layer,item2_source_file)
                        VALUES (@ctid,@guid,@name,@st,@dist,@desc,@grid,
                                @px,@py,@pz,@cd,@img,
                                @i1id,@i1n,@i1t,@i1l,@i1sf,
                                @i2id,@i2n,@i2t,@i2l,@i2sf)", conn))
                    {
                        cmd.Parameters.AddWithValue("ctid", res.ClashTestId);
                        cmd.Parameters.AddWithValue("guid", res.Guid ?? "");
                        cmd.Parameters.AddWithValue("name", res.Name ?? "");
                        cmd.Parameters.AddWithValue("st", res.Status ?? "");
                        cmd.Parameters.AddWithValue("dist", res.Distance.HasValue ? (object)res.Distance.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("desc", res.Description ?? "");
                        cmd.Parameters.AddWithValue("grid", res.GridLocation ?? "");
                        cmd.Parameters.AddWithValue("px", res.PointX.HasValue ? (object)res.PointX.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("py", res.PointY.HasValue ? (object)res.PointY.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("pz", res.PointZ.HasValue ? (object)res.PointZ.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("cd", res.CreatedDate.HasValue ? (object)res.CreatedDate.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("img", res.Image != null ? (object)res.Image : DBNull.Value);
                        cmd.Parameters.AddWithValue("i1id", res.Item1Id ?? "");
                        cmd.Parameters.AddWithValue("i1n", res.Item1Name ?? "");
                        cmd.Parameters.AddWithValue("i1t", res.Item1Type ?? "");
                        cmd.Parameters.AddWithValue("i1l", res.Item1Layer ?? "");
                        cmd.Parameters.AddWithValue("i1sf", res.Item1SourceFile ?? "");
                        cmd.Parameters.AddWithValue("i2id", res.Item2Id ?? "");
                        cmd.Parameters.AddWithValue("i2n", res.Item2Name ?? "");
                        cmd.Parameters.AddWithValue("i2t", res.Item2Type ?? "");
                        cmd.Parameters.AddWithValue("i2l", res.Item2Layer ?? "");
                        cmd.Parameters.AddWithValue("i2sf", res.Item2SourceFile ?? "");
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        // ── HISTORY ────────────────────────────────────────────────────
        public void UpsertClashTestHistory(int projectId, string testName,
            int total, int newCount, int active, int reviewed, int approved, int resolved)
        {
            using (var conn = new NpgsqlConnection(_cs))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(@"
                    INSERT INTO clash_test_history
                        (navisworks_project_id,test_name,record_date,
                         summary_total,summary_new,summary_active,
                         summary_reviewed,summary_approved,summary_resolved)
                    VALUES (@pid,@tn,CURRENT_DATE,@tot,@new,@act,@rev,@app,@res)
                    ON CONFLICT (navisworks_project_id,test_name,record_date) DO UPDATE SET
                        summary_total=EXCLUDED.summary_total,
                        summary_new=EXCLUDED.summary_new,
                        summary_active=EXCLUDED.summary_active,
                        summary_reviewed=EXCLUDED.summary_reviewed,
                        summary_approved=EXCLUDED.summary_approved,
                        summary_resolved=EXCLUDED.summary_resolved,
                        created_at=NOW()", conn))
                {
                    cmd.Parameters.AddWithValue("pid", projectId);
                    cmd.Parameters.AddWithValue("tn", testName);
                    cmd.Parameters.AddWithValue("tot", total);
                    cmd.Parameters.AddWithValue("new", newCount);
                    cmd.Parameters.AddWithValue("act", active);
                    cmd.Parameters.AddWithValue("rev", reviewed);
                    cmd.Parameters.AddWithValue("app", approved);
                    cmd.Parameters.AddWithValue("res", resolved);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── TEST CONNECTION ────────────────────────────────────────────
        public bool TestConnection()
        {
            try
            {
                using (var conn = new NpgsqlConnection(_cs))
                {
                    conn.Open();
                    return true;
                }
            }
            catch { return false; }
        }

        public void Dispose() { }
    }
}