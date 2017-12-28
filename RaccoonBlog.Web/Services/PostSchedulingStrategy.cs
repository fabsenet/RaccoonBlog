using System;
using System.Linq;
using System.Threading.Tasks;
using RaccoonBlog.Web.Infrastructure.Common;
using RaccoonBlog.Web.Models;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace RaccoonBlog.Web.Services
{
	/// <summary>
	/// The rules are simple:
	/// * If there is no set date, schedule in at the end of the queue, but on a Monday - Friday 
	/// * If there is a set date, move all the posts from that day one day forward
	///  * only to Monday - Friday
	///  * don't touch posts that are marked with SkipAutoReschedule = true
	/// * If we are moving a current post, we need to:
	///  * trim all the holes in the schedule
	/// </summary>
	public class PostSchedulingStrategy
	{
		private readonly IAsyncDocumentSession session;
		private readonly DateTimeOffset now;

		public PostSchedulingStrategy(IAsyncDocumentSession session, DateTimeOffset now)
		{
			this.session = session;
			this.now = now;
		}

		public DateTimeOffset Schedule()
		{
			var p = session.Query<Post>()
				.OrderByDescending(post => post.PublishAt)
				.Select(post => new {post.PublishAt})
				.FirstOrDefault();

			var lastScheduledPostDate = p == null || p.PublishAt < now ? now : p.PublishAt;
			return lastScheduledPostDate
				.AddDays(1)
				.SkipToNextWorkDay()
				.AtNoon();
		}

		public async Task<DateTimeOffset> Schedule(DateTimeOffset requestedDate)
		{
			var postsQuery = from p in session.Query<Post>().Include(x => x.CommentsId)
			                 where p.PublishAt > requestedDate && p.SkipAutoReschedule == false && p.PublishAt > now
			                 orderby p.PublishAt
			                 select p;

			var nextPostDate = requestedDate;
			foreach (var post in postsQuery)
			{
				nextPostDate
					= nextPostDate
						.AddDays(1)
						.SkipToNextWorkDay()
						.AtTime(post.PublishAt);

				post.PublishAt = nextPostDate;

			    if (post.CommentsId != null)
			    {
			        var postComments1 = await session.LoadAsync<PostComments>(post.CommentsId);
			        postComments1.Post.PublishAt = nextPostDate;
			    }
			}

			return requestedDate;
		}
	}
}