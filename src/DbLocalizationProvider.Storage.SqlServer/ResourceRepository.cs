using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace DbLocalizationProvider.Storage.SqlServer
{
    /// <summary>
    /// Repository for working with underlying MSSQL storage
    /// </summary>
    public class ResourceRepository
    {
        /// <summary>
        /// Gets all resources.
        /// </summary>
        /// <returns>List of resources</returns>
        public IEnumerable<LocalizationResource> GetAll()
        {
            using(var conn = new SqlConnection(Settings.DbContextConnectionString))
            {
                conn.Open();

                var cmd = new SqlCommand(@"
                    SELECT
                        r.Id,
                        ResourceKey,
                        Author,
                        FromCode,
                        IsHidden,
                        IsModified,
                        ModificationDate,
                        Notes,
                        t.Id as TranslationId,
                        t.Value as Translation,
                        t.Language
                    FROM [dbo].[LocalizationResources] r
                    LEFT JOIN [dbo].[LocalizationResourceTranslations] t ON r.Id = t.ResourceId",
                    conn);

                var reader = cmd.ExecuteReader();
                var lookup = new Dictionary<string, LocalizationResource>();

                while(reader.Read())
                {
                    var key = reader.GetString(reader.GetOrdinal(nameof(LocalizationResource.ResourceKey)));
                    if(lookup.TryGetValue(key, out var resource))
                    {
                        if (!reader.IsDBNull(reader.GetOrdinal("TranslationId")))
                        {
                            // add translation to already loaded resource
                            resource.Translations.Add(new LocalizationResourceTranslation
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("TranslationId")),
                                ResourceId = resource.Id,
                                Value = reader.GetStringSafe("Translation"),
                                Language = reader.GetString(reader.GetOrdinal("Language")),
                                LocalizationResource = resource
                            });
                        }
                    }
                    else
                    {
                        // create resource
                        var result = new LocalizationResource(key)
                        {
                            Id = reader.GetInt32(reader.GetOrdinal(nameof(LocalizationResource.Id))),
                            Author = reader.GetString(reader.GetOrdinal(nameof(LocalizationResource.Author))),
                            FromCode = reader.GetBoolean(reader.GetOrdinal(nameof(LocalizationResource.FromCode))),
                            IsHidden = reader.GetBoolean(reader.GetOrdinal(nameof(LocalizationResource.IsHidden))),
                            IsModified = reader.GetBoolean(reader.GetOrdinal(nameof(LocalizationResource.IsModified))),
                            ModificationDate = reader.GetDateTime(reader.GetOrdinal(nameof(LocalizationResource.ModificationDate))),
                            Notes = reader.GetStringSafe(nameof(LocalizationResource.Notes)),
                        };

                        if (!reader.IsDBNull(reader.GetOrdinal("TranslationId")))
                        {
                            result.Translations.Add(new LocalizationResourceTranslation
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("TranslationId")),
                                ResourceId = result.Id,
                                Value = reader.GetStringSafe("Translation"),
                                Language = reader.GetString(reader.GetOrdinal("Language")),
                                LocalizationResource = result
                            });
                        }

                        lookup.Add(key, result);
                    }
                }

                return lookup.Values;
            }
        }

        /// <summary>
        /// Gets resource by the key.
        /// </summary>
        /// <param name="resourceKey">The resource key.</param>
        /// <returns>Localized resource if found by given key</returns>
        /// <exception cref="ArgumentNullException">resourceKey</exception>
        public LocalizationResource GetByKey(string resourceKey)
        {
            if(resourceKey == null) throw new ArgumentNullException(nameof(resourceKey));

            using(var conn = new SqlConnection(Settings.DbContextConnectionString))
            {
                conn.Open();

                var cmd = new SqlCommand(@"
                    SELECT
                        r.Id,
                        Author,
                        FromCode,
                        IsHidden,
                        IsModified,
                        ModificationDate,
                        Notes,
                        t.Id as TranslationId,
                        t.Value as Translation,
                        t.Language
                    FROM [dbo].[LocalizationResources] r
                    LEFT JOIN [dbo].[LocalizationResourceTranslations] t ON r.Id = t.ResourceId
                    WHERE ResourceKey = @key",
                    conn);
                cmd.Parameters.AddWithValue("key", resourceKey);

                var reader = cmd.ExecuteReader();

                if(!reader.Read()) return null;

                var result = new LocalizationResource(resourceKey)
                {
                    Id = reader.GetInt32(reader.GetOrdinal(nameof(LocalizationResource.Id))),
                    Author = reader.GetString(reader.GetOrdinal(nameof(LocalizationResource.Author))),
                    FromCode = reader.GetBoolean(reader.GetOrdinal(nameof(LocalizationResource.FromCode))),
                    IsHidden = reader.GetBoolean(reader.GetOrdinal(nameof(LocalizationResource.IsHidden))),
                    IsModified = reader.GetBoolean(reader.GetOrdinal(nameof(LocalizationResource.IsModified))),
                    ModificationDate = reader.GetDateTime(reader.GetOrdinal(nameof(LocalizationResource.ModificationDate))),
                    Notes = reader.GetStringSafe(nameof(LocalizationResource.Notes))
                };

                // read 1st translation
                // if TranslationId is NULL - there is no translations for given resource
                if (!reader.IsDBNull(reader.GetOrdinal("TranslationId")))
                {
                    result.Translations.Add(new LocalizationResourceTranslation
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("TranslationId")),
                        ResourceId = result.Id,
                        Value = reader.GetStringSafe("Translation"),
                        Language = reader.GetString(reader.GetOrdinal("Language")),
                        LocalizationResource = result
                    });

                    while (reader.Read())
                    {
                        result.Translations.Add(new LocalizationResourceTranslation
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("TranslationId")),
                            ResourceId = result.Id,
                            Value = reader.GetStringSafe("Translation"),
                            Language = reader.GetString(reader.GetOrdinal("Language")),
                            LocalizationResource = result
                        });
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Adds the translation for the resource.
        /// </summary>
        /// <param name="resource">The resource.</param>
        /// <param name="translation">The translation.</param>
        /// <exception cref="ArgumentNullException">
        /// resource
        /// or
        /// translation
        /// </exception>
        public void AddTranslation(LocalizationResource resource, LocalizationResourceTranslation translation)
        {
            if(resource == null) throw new ArgumentNullException(nameof(resource));
            if(translation == null) throw new ArgumentNullException(nameof(translation));

            using(var conn = new SqlConnection(Settings.DbContextConnectionString))
            {
                conn.Open();

                var cmd = new SqlCommand("INSERT INTO [dbo].[LocalizationResourceTranslations] ([Language], [ResourceId], [Value]) VALUES (@language, @resourceId, @translation)", conn);
                cmd.Parameters.AddWithValue("language", translation.Language);
                cmd.Parameters.AddWithValue("resourceId", translation.ResourceId);
                cmd.Parameters.AddWithValue("translation", translation.Value);

                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Updates the translation for the resource.
        /// </summary>
        /// <param name="resource">The resource.</param>
        /// <param name="translation">The translation.</param>
        /// <exception cref="ArgumentNullException">
        /// resource
        /// or
        /// translation
        /// </exception>
        public void UpdateTranslation(LocalizationResource resource, LocalizationResourceTranslation translation)
        {
            if(resource == null) throw new ArgumentNullException(nameof(resource));
            if(translation == null) throw new ArgumentNullException(nameof(translation));

            using(var conn = new SqlConnection(Settings.DbContextConnectionString))
            {
                conn.Open();

                var cmd = new SqlCommand("UPDATE [dbo].[LocalizationResourceTranslations] SET [Value] = @translation WHERE [Id] = @id", conn);
                cmd.Parameters.AddWithValue("translation", translation.Value);
                cmd.Parameters.AddWithValue("id", translation.Id);

                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Deletes the translation.
        /// </summary>
        /// <param name="resource">The resource.</param>
        /// <param name="translation">The translation.</param>
        /// <exception cref="ArgumentNullException">
        /// resource
        /// or
        /// translation
        /// </exception>
        public void DeleteTranslation(LocalizationResource resource, LocalizationResourceTranslation translation)
        {
            if(resource == null) throw new ArgumentNullException(nameof(resource));
            if(translation == null) throw new ArgumentNullException(nameof(translation));

            using(var conn = new SqlConnection(Settings.DbContextConnectionString))
            {
                conn.Open();

                var cmd = new SqlCommand("DELETE FROM [dbo].[LocalizationResourceTranslations] WHERE [Id] = @id", conn);
                cmd.Parameters.AddWithValue("id", translation.Id);

                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Updates the resource.
        /// </summary>
        /// <param name="resource">The resource.</param>
        /// <exception cref="ArgumentNullException">resource</exception>
        public void UpdateResource(LocalizationResource resource)
        {
            if(resource == null) throw new ArgumentNullException(nameof(resource));

            using(var conn = new SqlConnection(Settings.DbContextConnectionString))
            {
                conn.Open();

                var cmd = new SqlCommand("UPDATE [dbo].[LocalizationResources] SET [IsModified] = @isModified, [ModificationDate] = @ModificationDate, [Notes] = @notes WHERE [Id] = @id", conn);
                cmd.Parameters.AddWithValue("id", resource.Id);
                cmd.Parameters.AddWithValue("modificationDate", resource.ModificationDate);
                cmd.Parameters.AddWithValue("isModified", resource.IsModified);
                cmd.Parameters.AddWithValue("notes", (object)resource.Notes ?? DBNull.Value);

                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Deletes the resource.
        /// </summary>
        /// <param name="resource">The resource.</param>
        /// <exception cref="ArgumentNullException">resource</exception>
        public void DeleteResource(LocalizationResource resource)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));

            using (var conn = new SqlConnection(Settings.DbContextConnectionString))
            {
                conn.Open();

                var cmd = new SqlCommand("DELETE FROM [dbo].[LocalizationResources] WHERE [Id] = @id", conn);
                cmd.Parameters.AddWithValue("id", resource.Id);

                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Deletes all resources. DANGEROUS!
        /// </summary>
        public void DeleteAllResources()
        {
            using (var conn = new SqlConnection(Settings.DbContextConnectionString))
            {
                conn.Open();

                var cmd = new SqlCommand("DELETE FROM [dbo].[LocalizationResources]", conn);

                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Inserts the resource in database.
        /// </summary>
        /// <param name="resource">The resource.</param>
        /// <exception cref="ArgumentNullException">resource</exception>
        public void InsertResource(LocalizationResource resource)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));

            using (var conn = new SqlConnection(Settings.DbContextConnectionString))
            {
                conn.Open();

                var cmd = new SqlCommand("INSERT INTO [dbo].[LocalizationResources] ([ResourceKey], [Author], [FromCode], [IsHidden], [IsModified], [ModificationDate], [Notes]) VALUES (@resourceKey, @author, @fromCode, @isHidden, @isModified, @modificationDate, @notes)", conn);

                cmd.Parameters.AddWithValue("resourceKey", resource.ResourceKey);
                cmd.Parameters.AddWithValue("author", resource.Author ?? "unknown");
                cmd.Parameters.AddWithValue("fromCode", resource.FromCode);
                cmd.Parameters.AddWithValue("isHidden", resource.IsHidden);
                cmd.Parameters.AddWithValue("isModified", resource.IsModified);
                cmd.Parameters.AddWithValue("modificationDate", resource.ModificationDate);
                cmd.Parameters.AddSafeWithValue("notes", resource.Notes);

                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Gets the available languages (reads in which languages translations are added).
        /// </summary>
        /// <param name="includeInvariant">if set to <c>true</c> [include invariant].</param>
        /// <returns></returns>
        public IEnumerable<CultureInfo> GetAvailableLanguages(bool includeInvariant)
        {
            using (var conn = new SqlConnection(Settings.DbContextConnectionString))
            {
                conn.Open();

                var cmd = new SqlCommand("SELECT DISTINCT [Language] FROM [dbo].[LocalizationResourceTranslations] WHERE [Language] <> ''", conn);
                var reader = cmd.ExecuteReader();

                var result = new List<CultureInfo>();
                if (includeInvariant) result.Add(CultureInfo.InvariantCulture);

                while (reader.Read())
                {
                    result.Add(new CultureInfo(reader.GetString(0)));
                }

                return result;
            }
        }
    }
}
