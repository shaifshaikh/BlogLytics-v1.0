using Bloglytics.DTO;
using Bloglytics.Repository;
using Microsoft.Data.SqlClient;

namespace Bloglytics.Repository
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly string _connectionString;

        public CategoryRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<IEnumerable<Category>> GetAllCategoriesAsync()
        {
            var categories = new List<Category>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        c.CategoryId, c.CategoryName, c.Description, c.IsActive, c.CreatedAt,
                        COUNT(bp.PostId) AS PostCount
                    FROM Categories c
                    LEFT JOIN BlogPosts bp ON c.CategoryId = bp.CategoryId AND bp.Status = 'Published'
                    GROUP BY c.CategoryId, c.CategoryName, c.Description, c.IsActive, c.CreatedAt
                    ORDER BY c.CategoryName";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            categories.Add(new Category
                            {
                                CategoryId = Convert.ToInt32(reader["CategoryId"]),
                                CategoryName = reader["CategoryName"].ToString(),
                                Description = reader["Description"]?.ToString(),
                                IsActive = Convert.ToBoolean(reader["IsActive"]),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                                PostCount = Convert.ToInt32(reader["PostCount"])
                            });
                        }
                    }
                }
            }

            return categories;
        }

        public async Task<IEnumerable<Category>> GetActiveCategoriesAsync()
        {
            var categories = new List<Category>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        c.CategoryId, c.CategoryName, c.Description, c.IsActive, c.CreatedAt,
                        COUNT(bp.PostId) AS PostCount
                    FROM Categories c
                    LEFT JOIN BlogPosts bp ON c.CategoryId = bp.CategoryId AND bp.Status = 'Published'
                    WHERE c.IsActive = 1
                    GROUP BY c.CategoryId, c.CategoryName, c.Description, c.IsActive, c.CreatedAt
                    ORDER BY c.CategoryName";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            categories.Add(new Category
                            {
                                CategoryId = Convert.ToInt32(reader["CategoryId"]),
                                CategoryName = reader["CategoryName"].ToString(),
                                Description = reader["Description"]?.ToString(),
                                IsActive = Convert.ToBoolean(reader["IsActive"]),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                                PostCount = Convert.ToInt32(reader["PostCount"])
                            });
                        }
                    }
                }
            }

            return categories;
        }

        public async Task<Category> GetCategoryByIdAsync(int categoryId)
        {
            Category category = null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT CategoryId, CategoryName, Description, IsActive, CreatedAt
                    FROM Categories
                    WHERE CategoryId = @CategoryId";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CategoryId", categoryId);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            category = new Category
                            {
                                CategoryId = Convert.ToInt32(reader["CategoryId"]),
                                CategoryName = reader["CategoryName"].ToString(),
                                Description = reader["Description"]?.ToString(),
                                IsActive = Convert.ToBoolean(reader["IsActive"]),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                            };
                        }
                    }
                }
            }

            return category;
        }

        public async Task<Category> GetCategoryByNameAsync(string categoryName)
        {
            Category category = null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT CategoryId, CategoryName, Description, IsActive, CreatedAt
                    FROM Categories
                    WHERE CategoryName = @CategoryName";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CategoryName", categoryName);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            category = new Category
                            {
                                CategoryId = Convert.ToInt32(reader["CategoryId"]),
                                CategoryName = reader["CategoryName"].ToString(),
                                Description = reader["Description"]?.ToString(),
                                IsActive = Convert.ToBoolean(reader["IsActive"]),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                            };
                        }
                    }
                }
            }

            return category;
        }

        public async Task<int> CreateCategoryAsync(Category category)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    INSERT INTO Categories (CategoryName, Description, IsActive, CreatedAt)
                    VALUES (@CategoryName, @Description, @IsActive, @CreatedAt);
                    SELECT CAST(SCOPE_IDENTITY() as int)";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CategoryName", category.CategoryName);
                    cmd.Parameters.AddWithValue("@Description", category.Description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsActive", category.IsActive);
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                    int categoryId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    return categoryId;
                }
            }
        }

        public async Task<bool> UpdateCategoryAsync(Category category)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    UPDATE Categories 
                    SET CategoryName = @CategoryName, Description = @Description, IsActive = @IsActive
                    WHERE CategoryId = @CategoryId";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CategoryId", category.CategoryId);
                    cmd.Parameters.AddWithValue("@CategoryName", category.CategoryName);
                    cmd.Parameters.AddWithValue("@Description", category.Description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsActive", category.IsActive);

                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        public async Task<bool> DeleteCategoryAsync(int categoryId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "DELETE FROM Categories WHERE CategoryId = @CategoryId";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CategoryId", categoryId);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        public async Task<int> GetBlogCountByCategoryAsync(int categoryId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "SELECT COUNT(*) FROM BlogPosts WHERE CategoryId = @CategoryId AND Status = 'Published'";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CategoryId", categoryId);
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
        }
    }
}