/*using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HibernatingRhinos.Loci.Common.Extensions;
using HibernatingRhinos.Loci.Common.Models;
using Microsoft.AspNetCore.Mvc;
using RaccoonBlog.Web.Infrastructure.AutoMapper;
using RaccoonBlog.Web.Infrastructure.Common;
using RaccoonBlog.Web.Models;
using RaccoonBlog.Web.Services;
using RaccoonBlog.Web.ViewModels;
using Raven.Client.Documents.Operations;

namespace RaccoonBlog.Web.Areas.Admin.Controllers
{
	public class PostsController : AdminController
	{
		public ActionResult Index()
		{
			// the actual UI is handled via JavaScript
			return View("List");
		}

		[HttpGet]
		public ActionResult Add()
		{
			return View("Edit", new PostInput
			{
				AllowComments = true,
				ContentType = DynamicContentType.Html,
				CreatedAt = DateTimeOffset.Now,
				PublishAt = null // force auto schedule
			});
		}

		[HttpGet]
		public ActionResult Edit(int id)
		{
			var post = RavenSession.Load<Post>("posts/" + id);
			if (post == null)
				return NotFound("Post does not exist.");
			return View(post.MapTo<PostInput>());
		}

		[HttpPost]
		public ActionResult Update(PostInput input)
		{
			if (!ModelState.IsValid)
				return View("Edit", input);

			var post = RavenSession.Load<Post>("posts/" + input.Id) ?? new Post {CreatedAt = DateTimeOffset.Now};
			input.MapPropertiesToInstance(post);

			// Be able to record the user making the actual post
			var user = RavenSession.GetCurrentUser();
			if (string.IsNullOrEmpty(post.AuthorId))
			{
				post.AuthorId = user.Id;
			}
			else
			{
				post.LastEditedByUserId = user.Id;
				post.LastEditedAt = DateTimeOffset.Now;
			}

			if (post.PublishAt == DateTimeOffset.MinValue)
			{
				var postScheduleringStrategy = new PostSchedulingStrategy(RavenSession, DateTimeOffset.Now);
				post.PublishAt = postScheduleringStrategy.Schedule();
			}

			// Actually save the post now
			RavenSession.Store(post);

			if (input.IsNewPost())
			{
				// Create the post comments object and link between it and the post
				var comments = new PostComments
				               {
				               	Comments = new List<PostComments.Comment>(),
				               	Spam = new List<PostComments.Comment>(),
				               	Post = new PostComments.PostReference
				               	       {
				               	       	Id = post.Id,
				               	       	PublishAt = post.PublishAt,
				               	       }
				               };

				RavenSession.Store(comments);
				post.CommentsId = comments.Id;	
			}

			return RedirectToAction("Details", new {Id = post.MapTo<PostReference>().DomainId});
		}

		public ActionResult Details(int id)
		{
			var post = RavenSession
				.Include<Post>(x => x.CommentsId)
				.Load("posts/" + id);

			if (post == null)
				return NotFound();

			var comments = RavenSession.Load<PostComments>(post.CommentsId);

			var vm = new AdminPostDetailsViewModel
			         {
			         	Post = post.MapTo<AdminPostDetailsViewModel.PostDetails>(),

			         	Comments = comments.Comments
			         		.Concat(comments.Spam)
			         		.OrderBy(comment => comment.CreatedAt)
			         		.MapTo<AdminPostDetailsViewModel.Comment>(),

			         	NextPost = RavenSession.GetNextPrevPost(post, true),
			         	PreviousPost = RavenSession.GetNextPrevPost(post, false),
			         	AreCommentsClosed = comments.AreCommentsClosed(post, BlogConfig.NumberOfDayToCloseComments),
			         };

			return View("Details", vm);
		}

		public ActionResult ListFeed(DateTime start, DateTime end)
		{
			var posts = RavenSession.Query<Post>()
				.Where
				(
					post => post.PublishAt >= start &&
							post.PublishAt <= end
				)
				.OrderBy(post => post.PublishAt)
				.Take(256)
				.ToList();

			return Json(posts.MapTo<PostSummaryJson>());
		}


		[HttpPost]
		public ActionResult SetPostDate(int id, long date)
		{
			var post = RavenSession
				.Include<Post>(x => x.CommentsId)
				.Load("posts/" + id);
			if (post == null)
				return Json(new {success = false});

			post.PublishAt = post.PublishAt.WithDate(DateTimeOffsetUtil.ConvertFromJsTimestamp(date));
			RavenSession.Load<PostComments>(post.CommentsId).Post.PublishAt = post.PublishAt;

			return Json(new {success = true});
		}

		[HttpPost]
		public ActionResult CommentsAdmin(int id, CommentCommandOptions command, int[] commentIds)
		{
			if (commentIds == null || commentIds.Length == 0)
				ModelState.AddModelError("CommentIdsAreEmpty", "Not comments was selected.");

			var post = RavenSession.Load<Post>("posts/" + id);
			if (post == null)
				return NotFound();

			if (ModelState.IsValid == false)
			{
				if (Request.IsAjaxRequest())
					return Json(new {Success = false, message = ModelState.FirstErrorMessage()});

				return Details(id);
			}

			var comments = RavenSession.Load<PostComments>(post.CommentsId);
			switch (command)
			{
				case CommentCommandOptions.Delete:
					comments.Comments.RemoveAll(c => commentIds.Contains(c.Id));
					comments.Spam.RemoveAll(c => commentIds.Contains(c.Id));
					break;

				case CommentCommandOptions.MarkSpam:
					var spams = comments.Comments.Concat(comments.Spam)
						.Where(c => commentIds.Contains(c.Id))
						.ToArray();

					comments.Comments.RemoveAll(spams.Contains);
					comments.Spam.RemoveAll(spams.Contains);
					foreach (var comment in spams)
					{
						AkismetService.MarkSpam(comment);
					}
					break;

				case CommentCommandOptions.MarkHam:
					var ham = comments.Spam
						.Where(c => commentIds.Contains(c.Id))
						.ToArray();

					comments.Spam.RemoveAll(ham.Contains);
					comments.Comments.AddRange(ham);

					comments.Comments
						.Where(c => c.IsSpam)
						.ForEach(comment =>
						         	{
						         		comment.IsSpam = false;
						         		AkismetService.MarkHam(comment);
						         		ResetNumberOfSpamComments(comment);
						         	});
					break;
				default:
					throw new InvalidOperationException(command + " command is not recognized.");
			}

			post.CommentsCount = comments.Comments.Count;

			if (Request.IsAjaxRequest())
			{
				return Json(new {Success = true});
			}
			return RedirectToAction("Details", new {id});
		}

		private void ResetNumberOfSpamComments(PostComments.Comment comment)
		{
			if (comment.CommenterId == null) 
				return;
			var commenter = RavenSession.Load<Commenter>(comment.CommenterId);
			if (commenter == null) 
				return;
			commenter.NumberOfSpamComments = 0;
		}

		[HttpPost]
		public ActionResult Delete(int id)
		{
            var post = RavenSession.Load<Post>("posts/" + id);
		    if (post == null)
		        return SuccessResponse();

		    if (string.IsNullOrEmpty(post.CommentsId) == false)
            {
                RavenSession.Delete(post.CommentsId);
            }

            RavenSession.Delete(post);

            return SuccessResponse();
        }

        private ActionResult SuccessResponse()
        {
            if (Request.IsAjaxRequest())
            {
                return Json(new { Success = true });
            }
            return RedirectToAction("Index");
        }

        [HttpGet]
		public ActionResult DeleteAllSpamComments()
		{
			return View();
		}

		[HttpPost]
		public async Task<ActionResult> DeleteAllSpamCommentsAsync(bool deleteAll)
		{
		    await DocumentStore.Operations.SendAsync(new PatchByQueryOperation(@"
from PostComments
where Spam.Count > 0
update {
    this.Spam = [];
}
"));
			return View();
		}
	}

	public enum CommentCommandOptions
	{
		Delete,
		MarkHam,
		MarkSpam
	}
}*/