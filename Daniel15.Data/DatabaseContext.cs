﻿using System.Data.Entity;
using System.Data.Entity.Infrastructure.DependencyResolution;
using System.Data.Entity.Infrastructure.Pluralization;
using System.Text.RegularExpressions;
using Daniel15.Data.Entities.Blog;
using Daniel15.Data.Entities.Projects;
using Daniel15.Shared.Extensions;

namespace Daniel15.Data
{
	/// <summary>
	/// Entity Framework database context
	/// </summary>
	public class DatabaseContext : DbContext
	{
		/// <summary>
		/// Prefix for boolean fields in the database
		/// </summary>
		private const string BOOLEAN_PREFIX = "Is_";
		/// <summary>
		/// Suffix for model classes
		/// </summary>
		private const string MODEL_SUFFIX = "Model";
		/// <summary>
		/// Prefix for "raw" fields, used when the database representation is different (ie. EF doesn't
		/// support a particular field format)
		/// </summary>
		private const string RAW_PREFIX = "Raw_";
		/// <summary>
		/// Regular expression matching segments in a camelcase string
		/// </summary>
		private static readonly Regex _camelCaseRegex = new Regex(".[A-Z]");

		/// <summary>
		/// Creates a new instance of <see cref="DatabaseContext"/>.
		/// </summary>
		public DatabaseContext() : base("name=Database") { }

		/// <summary>
		/// Projects in the database.
		/// </summary>
		public virtual DbSet<ProjectModel> Projects { get; set; }
		/// <summary>
		/// Technologies used to build projects.
		/// </summary>
		public virtual DbSet<ProjectTechnologyModel> Technologies { get; set; }
		/// <summary>
		/// Blog posts.
		/// </summary>
		public virtual DbSet<PostModel> Posts { get; set; }
		/// <summary>
		/// Blog categories.
		/// </summary>
		public virtual DbSet<CategoryModel> Categories { get; set; }
		/// <summary>
		/// Blog tags.
		/// </summary>
		public virtual DbSet<TagModel> Tags { get; set; }
		/// <summary>
		/// Comments synchronised from Disqus.
		/// </summary>
		public virtual DbSet<DisqusCommentModel> DisqusComments { get; set; }

		/// <summary>
		/// Initialises the Entity Framework model
		/// </summary>
		/// <param name="modelBuilder">EF model builder</param>
		protected override void OnModelCreating(DbModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			ConfigureConventions(modelBuilder);
			ConfigureManyToMany(modelBuilder);

			// Special cases
			modelBuilder.Entity<ProjectTechnologyModel>().ToTable("project_techs");
			modelBuilder.Entity<PostModel>().ToTable("blog_posts");
			modelBuilder.Entity<PostModel>().Property(x => x.MainCategoryId).HasColumnName("maincategory_id");
			modelBuilder.Entity<CategoryModel>().ToTable("blog_categories");
			modelBuilder.Entity<CategoryModel>().Property(x => x.ParentId).HasColumnName("parent_category_id");
			modelBuilder.Entity<TagModel>().ToTable("blog_tags");
			modelBuilder.Entity<TagModel>().Property(x => x.ParentId).HasColumnName("parent_tag_id");
			modelBuilder.Entity<DisqusCommentModel>()
				.ToTable("disqus_comments")
				.Ignore(x => x.Children);

			// Backwards compatibility with old DB - Dates as UNIX times
			modelBuilder.Entity<PostModel>().Ignore(x => x.Date);
			modelBuilder.Entity<PostModel>().Property(x => x.UnixDate).HasColumnName("date");

			// Entity Framework hacks - Data types like enums that need backing fields
			modelBuilder.Entity<ProjectModel>()
				.Ignore(x => x.ProjectType)
				.Ignore(x => x.Technologies);
			modelBuilder.Entity<ProjectModel>().Property(x => x.RawProjectType).HasColumnName("type");
		}

		/// <summary>
		/// Configures standard conventions for names of the database tables and fields
		/// </summary>
		/// <param name="modelBuilder">EF model builder</param>
		private void ConfigureConventions(DbModelBuilder modelBuilder)
		{
			// Remove "Model" suffix for models ("ProjectModel" -> "projects")
			var pluralizationService = DbConfiguration.DependencyResolver.GetService<IPluralizationService>();
			modelBuilder.Types().Configure(config =>
			{
				var cleanName = config.ClrType.Name.Replace(MODEL_SUFFIX, string.Empty).ToLowerInvariant();
				config.ToTable(pluralizationService.Pluralize(cleanName));
			});

			modelBuilder.Properties().Configure(config =>
			{
				// Use underscores for column names (eg. "AuthorProfileUrl" -> "author_profile_url"
				var name = _camelCaseRegex.Replace(
					config.ClrPropertyInfo.Name,
					match => match.Value[0] + "_" + match.Value[1]
				);
				// Remove "is" prefix (eg. "IsPrimary" -> "Primary") and "Raw" prefix (eg. "RawTechnologies" => "Technologies")
				name = name.TrimStart(BOOLEAN_PREFIX).TrimStart(RAW_PREFIX).ToLowerInvariant();
				config.HasColumnName(name);
			});
		}

		/// <summary>
		/// Configures many to many relationships
		/// </summary>
		/// <param name="modelBuilder">EF model builder</param>
		private void ConfigureManyToMany(DbModelBuilder modelBuilder)
		{
			// Posts to categories many to many
			modelBuilder.Entity<PostModel>()
				.HasMany(x => x.Categories)
				.WithMany(x => x.Posts)
				.Map(map => map
					.MapLeftKey("post_id")
					.MapRightKey("category_id")
					.ToTable("blog_post_categories")
				);
			// Posts to tags many to many
			modelBuilder.Entity<PostModel>()
				.HasMany(x => x.Tags)
				.WithMany(x => x.Posts)
				.Map(map => map
					.MapLeftKey("post_id")
					.MapRightKey("tag_id")
					.ToTable("blog_post_tags")
				);
		}
	}
}
