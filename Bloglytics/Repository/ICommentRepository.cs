using Bloglytics.DTO;
using Bloglytics.Models;

namespace Bloglytics.Repository
{
    public interface ICommentRepository
    {
        Task<IEnumerable<Comment>> GetCommentsByBlogIdAsync(int blogId);
        Task<IEnumerable<Comment>> GetPendingCommentsAsync();
        Task<Comment> GetCommentByIdAsync(int commentId);
        Task<int> CreateCommentAsync(Comment comment);
        Task<bool> UpdateCommentAsync(Comment comment);
        Task<bool> DeleteCommentAsync(int commentId);
        Task<bool> ApproveCommentAsync(int commentId);
        Task<bool> RejectCommentAsync(int commentId);
        Task<int> GetCommentsCountByBlogIdAsync(int blogId);
    }
}