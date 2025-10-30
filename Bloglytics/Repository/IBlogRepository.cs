using Bloglytics.DTO;
using Bloglytics.Models;
using System.Reflection.Metadata;

namespace Bloglytics.Repository
{
    public interface IBlogRepository
    {
        // Get Operations
        Task<IEnumerable<Blog>> GetAllPublishedBlogsAsync(int page = 1, int pageSize = 9);
        Task<IEnumerable<Blog>> GetBlogsByUserIdAsync(int userId);
        Task<IEnumerable<Blog>> GetBlogsByCategoryAsync(string categoryName, int page = 1, int pageSize = 9);
        Task<Blog> GetBlogByIdAsync(int blogId);
        Task<Blog> GetBlogBySlugAsync(string slug);
        Task<int> GetTotalBlogsCountAsync();
        Task<int> GetTotalBlogsByUserIdAsync(int userId);

        // Create, Update, Delete
        Task<int> CreateBlogAsync(Blog blog);
        Task<bool> UpdateBlogAsync(Blog blog);
        Task<bool> DeleteBlogAsync(int blogId);

        // Trending & Popular
        Task<IEnumerable<Blog>> GetTrendingBlogsAsync(int count = 10);
        Task<IEnumerable<Blog>> GetPopularBlogsAsync(int count = 5);
        Task<IEnumerable<Blog>> GetRecentBlogsAsync(int count = 5);

        // Search
        Task<IEnumerable<Blog>> SearchBlogsAsync(string keyword, int page = 1, int pageSize = 9);

        // Engagement
        Task<bool> IncrementViewCountAsync(int blogId);
        Task<bool> LikeBlogAsync(int blogId, int userId);
        Task<bool> UnlikeBlogAsync(int blogId, int userId);
        Task<bool> HasUserLikedBlogAsync(int blogId, int userId);
        Task<int> GetLikesCountAsync(int blogId);

        // Admin Operations
        Task<IEnumerable<Blog>> GetAllBlogsForAdminAsync(int page = 1, int pageSize = 20);
        Task<bool> ChangeStatusAsync(int blogId, string status);
    }
}