﻿namespace RaccoonBlog.Web.ViewModels
{
    using System;
    using System.Collections.Generic;
    using RaccoonBlog.Web.Infrastructure.Common;

    public class SeriesPostsViewModel
    {
        public SeriesPostsViewModel()
        {
            SeriesInfo = new List<SeriesInfo>();
        }
        public int CurrentPage { get; set; }
        public int PostsCount { get; set; }
        public int PageSize { get; set; }
        public IList<SeriesInfo> SeriesInfo { get; set; }
    }

    public class SeriesInfo
    {
        public SeriesInfo()
        {
            PostsInSeries = new List<PostInSeries>();
        }

        public int SeriesId { get; set; }
        public string SeriesTitle { get; set; }
        public IList<PostInSeries> PostsInSeries { get; set; }

        private string _seriesSlug;
        public string SeriesSlug
        {
            get => _seriesSlug ?? (_seriesSlug = SlugConverter.TitleToSlug(SeriesTitle));
            set => _seriesSlug = value;
        }
    }

    public class PostInSeries
    {
        public int Id { get; set; }
        public string Slug { get; set; }
        public DateTimeOffset PublishAt { get; set; }
        public string Title { get; set; }

		public string Time
		{
			get
			{
				var totalMinutes = (PublishAt - DateTimeOffset.Now).TotalMinutes;

				if (totalMinutes < 0)
					throw new InvalidOperationException(string.Format("Future post error: the post is already published. Post Id: {0}, PublishAt: {1}, Now: {2}", Title, PublishAt, DateTimeOffset.Now));

				return TimeConverter.DistanceOfTimeInWords(totalMinutes) + " from now";
			}
		}
    }
}