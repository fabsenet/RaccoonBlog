using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Html;

namespace RaccoonBlog.Web.ViewModels
{
	public class AdminPostDetailsViewModel
	{
		public PostReference PreviousPost { get; set; }
		public PostReference NextPost { get; set; }

		public PostDetails Post { get; set; }
		public IList<Comment> Comments { get; set; }

		public bool AreCommentsClosed { get; set; }

		public class Comment
		{
			public int Id { get; set; }
			public HtmlString Body { get; set; }
			public string Author { get; set; }
			public string Url { get; set; }    // Look for HTML injection.
			public string EmailHash { get; set; }
			public DateTimeOffset CreatedAt { get; set; }
			public bool IsSpam { get; set; }
			public bool IsImportant { get; set; }
		}

		public class PostDetails
		{
			public int Id { get; set; }
			public HtmlString Title { get; set; }
			public string Slug { get; set; }
			public HtmlString Body { get; set; }

			public DateTimeOffset CreatedAt { get; set; }
			public DateTimeOffset PublishedAt { get; set; }

			public ICollection<string> Tags { get; set; }

			public string Key { get; set; }
		}
	}
}
