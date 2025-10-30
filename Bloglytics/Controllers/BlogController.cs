using Bloglytics.DTO;
using Bloglytics.Helpers;
using Bloglytics.Repository;
using Microsoft.AspNetCore.Mvc;

//using Microsoft.AspNetCore.Mvc;
//using Bloglytics.Models;
//using Bloglytics.Models.ViewModels;
//using Bloglytics.Repositories.Interfaces;
//using Bloglytics.Helpers;



namespace Bloglytics.Controllers
{
    public class BlogController : Controller
    {
        private readonly IBlogRepository _blogRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ICommentRepository _commentRepository;

        public BlogController(
            IBlogRepository blogRepository,
            ICategoryRepository categoryRepository,
            ICommentRepository commentRepository)
        {
            _blogRepository = blogRepository;
            _categoryRepository = categoryRepository;
            _commentRepository = commentRepository;
        }

        // GET: Blog/Index - List all published blogs
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, string category = null, string search = null)
        {
            try
            {
                int pageSize = 9;
                var model = new BlogListViewModel
                {
                    CurrentPage = page,
                    SelectedCategory = category,
                    SearchQuery = search
                };

                // Get categories for filter
                model.Categories = (await _categoryRepository.GetActiveCategoriesAsync()).ToList();

                // Get blogs based on filters
                IEnumerable<Blog> blogs;
                if (!string.IsNullOrEmpty(search))
                {
                    blogs = await _blogRepository.SearchBlogsAsync(search, page, pageSize);
                    model.TotalBlogs = blogs.Count();
                }
                else if (!string.IsNullOrEmpty(category))
                {
                    blogs = await _blogRepository.GetBlogsByCategoryAsync(category, page, pageSize);
                    model.TotalBlogs = blogs.Count();
                }
                else
                {
                    blogs = await _blogRepository.GetAllPublishedBlogsAsync(page, pageSize);
                    model.TotalBlogs = await _blogRepository.GetTotalBlogsCountAsync();
                }

                model.Blogs = blogs.ToList();
                model.TotalPages = (int)Math.Ceiling((double)model.TotalBlogs / pageSize);

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading blogs.";
                return View(new BlogListViewModel());
            }
        }

        // GET: Blog/MyBlogs - User's own blogs
        [HttpGet]
        public async Task<IActionResult> MyBlogs()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                int userId = int.Parse(userIdStr);
                var allBlogs = await _blogRepository.GetBlogsByUserIdAsync(userId);

                var model = new MyBlogsViewModel
                {
                    PublishedBlogs = allBlogs.Where(b => b.Status == "Published").ToList(),
                    DraftBlogs = allBlogs.Where(b => b.Status == "Draft").ToList(),
                    TotalViews = allBlogs.Sum(b => b.ViewCount),
                    TotalLikes = allBlogs.Sum(b => b.LikeCount),
                    TotalComments = allBlogs.Sum(b => b.CommentCount)
                };

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading your blogs.";
                return View(new MyBlogsViewModel());
            }
        }

        // GET: Blog/Create - Show create form
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.Categories = await _categoryRepository.GetActiveCategoriesAsync();
            return View(new CreateBlogViewModel());
        }

        // POST: Blog/Create - Submit new blog
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateBlogViewModel model)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _categoryRepository.GetActiveCategoriesAsync();
                return View(model);
            }

            try
            {
                int userId = int.Parse(userIdStr);

                // Upload image if provided
                string imagePath = null;
                if (model.FeaturedImage != null)
                {
                    imagePath = await FileUploadHelper.UploadImageAsync(
                        model.FeaturedImage,
                        "uploads/blogs",
                        userId);
                }

                // Create blog object
                var blog = new Blog
                {
                    Title = model.Title,
                    Slug = FileUploadHelper.GenerateSlug(model.Title),
                    Content = model.Content,
                    Summary = model.Summary,
                    FeaturedImage = imagePath,
                    AuthorId = userId,
                    CategoryId = model.CategoryId,
                    Status = model.Status,
                    CreatedAt = DateTime.Now
                };

                // Save to database
                int postId = await _blogRepository.CreateBlogAsync(blog);

                if (postId > 0)
                {
                    TempData["SuccessMessage"] = $"Blog '{model.Title}' created successfully!";
                    return RedirectToAction("MyBlogs");
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to create blog.";
                    ViewBag.Categories = await _categoryRepository.GetActiveCategoriesAsync();
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error creating blog: " + ex.Message;
                ViewBag.Categories = await _categoryRepository.GetActiveCategoriesAsync();
                return View(model);
            }
        }

        // GET: Blog/Edit/{id} - Show edit form
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var blog = await _blogRepository.GetBlogByIdAsync(id);

                if (blog == null)
                {
                    TempData["ErrorMessage"] = "Blog not found.";
                    return RedirectToAction("MyBlogs");
                }

                // Check if user can edit (owner or admin)
                int userId = int.Parse(userIdStr);
                if (blog.AuthorId != userId && userRole != "Admin")
                {
                    TempData["ErrorMessage"] = "You don't have permission to edit this blog.";
                    return RedirectToAction("MyBlogs");
                }

                var model = new EditBlogViewModel
                {
                    PostId = blog.PostId,
                    Title = blog.Title,
                    Content = blog.Content,
                    Summary = blog.Summary,
                    CategoryId = blog.CategoryId,
                    Status = blog.Status,
                    ExistingImage = blog.FeaturedImage
                };

                ViewBag.Categories = await _categoryRepository.GetActiveCategoriesAsync();
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading blog.";
                return RedirectToAction("MyBlogs");
            }
        }

        // POST: Blog/Edit - Update blog
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditBlogViewModel model)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _categoryRepository.GetActiveCategoriesAsync();
                return View(model);
            }

            try
            {
                int userId = int.Parse(userIdStr);
                var existingBlog = await _blogRepository.GetBlogByIdAsync(model.PostId);

                if (existingBlog == null)
                {
                    TempData["ErrorMessage"] = "Blog not found.";
                    return RedirectToAction("MyBlogs");
                }

                // Upload new image if provided
                string imagePath = model.ExistingImage;
                if (model.FeaturedImage != null)
                {
                    // Delete old image
                    if (!string.IsNullOrEmpty(model.ExistingImage))
                    {
                        FileUploadHelper.DeleteImage(model.ExistingImage);
                    }

                    imagePath = await FileUploadHelper.UploadImageAsync(
                        model.FeaturedImage,
                        "uploads/blogs",
                        userId);
                }

                // Update blog
                var blog = new Blog
                {
                    PostId = model.PostId,
                    Title = model.Title,
                    Slug = FileUploadHelper.GenerateSlug(model.Title),
                    Content = model.Content,
                    Summary = model.Summary,
                    FeaturedImage = imagePath,
                    CategoryId = model.CategoryId,
                    Status = model.Status,
                    UpdatedAt = DateTime.Now,
                    PublishedAt = existingBlog.PublishedAt
                };

                bool updated = await _blogRepository.UpdateBlogAsync(blog);

                if (updated)
                {
                    TempData["SuccessMessage"] = "Blog updated successfully!";
                    return RedirectToAction("MyBlogs");
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to update blog.";
                    ViewBag.Categories = await _categoryRepository.GetActiveCategoriesAsync();
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating blog: " + ex.Message;
                ViewBag.Categories = await _categoryRepository.GetActiveCategoriesAsync();
                return View(model);
            }
        }

        // POST: Blog/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userIdStr))
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var blog = await _blogRepository.GetBlogByIdAsync(id);

                if (blog == null)
                {
                    return Json(new { success = false, message = "Blog not found" });
                }

                // Check permissions
                int userId = int.Parse(userIdStr);
                if (blog.AuthorId != userId && userRole != "Admin")
                {
                    return Json(new { success = false, message = "Permission denied" });
                }

                // Delete image if exists
                if (!string.IsNullOrEmpty(blog.FeaturedImage))
                {
                    FileUploadHelper.DeleteImage(blog.FeaturedImage);
                }

                bool deleted = await _blogRepository.DeleteBlogAsync(id);

                if (deleted)
                {
                    return Json(new { success = true, message = "Blog deleted successfully" });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to delete blog" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // GET: Blog/Detail/{id} - View single blog
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            try
            {
                var blog = await _blogRepository.GetBlogByIdAsync(id);

                if (blog == null || blog.Status != "Published")
                {
                    TempData["ErrorMessage"] = "Blog not found.";
                    return RedirectToAction("Index");
                }

                // Increment view count
                await _blogRepository.IncrementViewCountAsync(id);

                // Get comments
                var comments = await _commentRepository.GetCommentsByBlogIdAsync(id);

                // Get related blogs
                var relatedBlogs = (await _blogRepository.GetAllPublishedBlogsAsync(1, 3))
                    .Where(b => b.CategoryId == blog.CategoryId && b.PostId != id)
                    .Take(3)
                    .ToList();

                // Check if user has liked
                bool hasLiked = false;
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (!string.IsNullOrEmpty(userIdStr))
                {
                    int userId = int.Parse(userIdStr);
                    hasLiked = await _blogRepository.HasUserLikedBlogAsync(id, userId);
                }

                var model = new BlogDetailViewModel
                {
                    Blog = blog,
                    Comments = comments.ToList(),
                    RelatedBlogs = relatedBlogs,
                    HasUserLiked = hasLiked,
                    LikeCount = await _blogRepository.GetLikesCountAsync(id),
                    CanEdit = !string.IsNullOrEmpty(userIdStr) &&
                             (blog.AuthorId == int.Parse(userIdStr) ||
                              HttpContext.Session.GetString("UserRole") == "Admin"),
                    CanDelete = !string.IsNullOrEmpty(userIdStr) &&
                               (blog.AuthorId == int.Parse(userIdStr) ||
                                HttpContext.Session.GetString("UserRole") == "Admin")
                };

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading blog.";
                return RedirectToAction("Index");
            }
        }

        // GET: Blog/Trending - Show trending blogs
        [HttpGet]
        public async Task<IActionResult> Trending()
        {
            try
            {
                var trendingBlogs = await _blogRepository.GetTrendingBlogsAsync(12);
                var categories = await _categoryRepository.GetActiveCategoriesAsync();

                ViewBag.Categories = categories;
                return View(trendingBlogs.ToList());
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading trending blogs.";
                return View(new List<Blog>());
            }
        }

        // POST: Blog/Like/{id} - Like/Unlike blog
        [HttpPost]
        public async Task<IActionResult> Like(int id)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userIdStr))
            {
                return Json(new { success = false, message = "Please login to like posts" });
            }

            try
            {
                int userId = int.Parse(userIdStr);
                bool hasLiked = await _blogRepository.HasUserLikedBlogAsync(id, userId);

                if (hasLiked)
                {
                    await _blogRepository.UnlikeBlogAsync(id, userId);
                }
                else
                {
                    await _blogRepository.LikeBlogAsync(id, userId);
                }

                int likeCount = await _blogRepository.GetLikesCountAsync(id);

                return Json(new
                {
                    success = true,
                    liked = !hasLiked,
                    likeCount = likeCount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error processing like" });
            }
        }

        // POST: Blog/AddComment - Add comment to blog
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(AddCommentViewModel model)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userIdStr))
            {
                TempData["ErrorMessage"] = "Please login to comment.";
                return RedirectToAction("Detail", new { id = model.PostId });
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Invalid comment.";
                return RedirectToAction("Detail", new { id = model.PostId });
            }

            try
            {
                int userId = int.Parse(userIdStr);

                var comment = new Comment
                {
                    PostId = model.PostId,
                    UserId = userId,
                    Content = model.Content,
                    ParentCommentId = model.ParentCommentId,
                    IsApproved = true, // Auto-approve for now
                    CreatedAt = DateTime.Now
                };

                int commentId = await _commentRepository.CreateCommentAsync(comment);

                if (commentId > 0)
                {
                    TempData["SuccessMessage"] = "Comment added successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to add comment.";
                }

                return RedirectToAction("Detail", new { id = model.PostId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error adding comment.";
                return RedirectToAction("Detail", new { id = model.PostId });
            }
        }
    }
}


