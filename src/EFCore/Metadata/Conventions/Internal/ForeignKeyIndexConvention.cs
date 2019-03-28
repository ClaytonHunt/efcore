// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics.Internal;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal
{
    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public class ForeignKeyIndexConvention :
        IForeignKeyAddedConvention,
        IForeignKeyRemovedConvention,
        IForeignKeyPropertiesChangedConvention,
        IForeignKeyUniquenessChangedConvention,
        IKeyAddedConvention,
        IKeyRemovedConvention,
        IEntityTypeBaseTypeChangedConvention,
        IIndexAddedConvention,
        IIndexRemovedConvention,
        IIndexUniquenessChangedConvention,
        IModelFinalizedConvention
    {
        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public ForeignKeyIndexConvention([NotNull] IDiagnosticsLogger<DbLoggerCategory.Model> logger)
        {
            Logger = logger;
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        protected virtual IDiagnosticsLogger<DbLoggerCategory.Model> Logger { get; }

        /// <summary>
        ///     Called after a foreign key is added to the entity type.
        /// </summary>
        /// <param name="relationshipBuilder"> The builder for the foreign key. </param>
        /// <param name="context"> Additional information associated with convention execution. </param>
        public virtual void ProcessForeignKeyAdded(
            IConventionRelationshipBuilder relationshipBuilder, IConventionContext<IConventionRelationshipBuilder> context)
        {
            var foreignKey = relationshipBuilder.Metadata;
            CreateIndex(foreignKey.Properties, foreignKey.IsUnique, foreignKey.DeclaringEntityType.Builder);
        }

        /// <summary>
        ///     Called after a foreign key is removed.
        /// </summary>
        /// <param name="entityTypeBuilder"> The builder for the entity type. </param>
        /// <param name="foreignKey"> The removed foreign key. </param>
        /// <param name="context"> Additional information associated with convention execution. </param>
        public virtual void ProcessForeignKeyRemoved(
            IConventionEntityTypeBuilder entityTypeBuilder, IConventionForeignKey foreignKey,
            IConventionContext<IConventionForeignKey> context)
        {
            OnForeignKeyRemoved(foreignKey.DeclaringEntityType, foreignKey.Properties);
        }

        /// <summary>
        ///     Called after the foreign key properties or principal key are changed.
        /// </summary>
        /// <param name="relationshipBuilder"> The builder for the foreign key. </param>
        /// <param name="oldDependentProperties"> The old foreign key properties. </param>
        /// <param name="oldPrincipalKey"> The old principal key. </param>
        /// <param name="context"> Additional information associated with convention execution. </param>
        public virtual void ProcessForeignKeyPropertiesChanged(
            IConventionRelationshipBuilder relationshipBuilder,
            IReadOnlyList<IConventionProperty> oldDependentProperties,
            IConventionKey oldPrincipalKey,
            IConventionContext<IConventionRelationshipBuilder> context)
        {
            var foreignKey = relationshipBuilder.Metadata;
            if (!foreignKey.Properties.SequenceEqual(oldDependentProperties))
            {
                OnForeignKeyRemoved(foreignKey.DeclaringEntityType, oldDependentProperties);
                if (relationshipBuilder.Metadata.Builder != null)
                {
                    CreateIndex(foreignKey.Properties, foreignKey.IsUnique, foreignKey.DeclaringEntityType.Builder);
                }
            }
        }

        private static void OnForeignKeyRemoved(IConventionEntityType declaringType, IReadOnlyList<IConventionProperty> foreignKeyProperties)
        {
            var index = declaringType.FindIndex(foreignKeyProperties);
            if (index == null)
            {
                return;
            }

            var otherForeignKeys = declaringType.FindForeignKeys(foreignKeyProperties).ToList();
            if (otherForeignKeys.Count != 0)
            {
                if (index.IsUnique
                    && otherForeignKeys.All(fk => !fk.IsUnique))
                {
                    index.Builder.IsUnique(false);
                }

                return;
            }

            index.DeclaringEntityType.Builder.HasNoIndex(index);
        }

        /// <summary>
        ///     Called after a key is added to the entity type.
        /// </summary>
        /// <param name="keyBuilder"> The builder for the key. </param>
        /// <param name="context"> Additional information associated with convention execution. </param>
        public virtual void ProcessKeyAdded(IConventionKeyBuilder keyBuilder, IConventionContext<IConventionKeyBuilder> context)
        {
            var key = keyBuilder.Metadata;
            foreach (var index in key.DeclaringEntityType.GetDerivedTypesInclusive()
                .SelectMany(t => t.GetDeclaredIndexes())
                .Where(i => AreIndexedBy(i.Properties, i.IsUnique, key.Properties, true)).ToList())
            {
                RemoveIndex(index);
            }
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual void ProcessKeyRemoved(
            IConventionEntityTypeBuilder entityTypeBuilder, IConventionKey key, IConventionContext<IConventionKey> context)
        {
            foreach (var otherForeignKey in key.DeclaringEntityType.GetDerivedTypesInclusive()
                .SelectMany(t => t.GetDeclaredForeignKeys())
                .Where(fk => AreIndexedBy(fk.Properties, fk.IsUnique, key.Properties, coveringIndexUniqueness: true)))
            {
                CreateIndex(otherForeignKey.Properties, otherForeignKey.IsUnique, otherForeignKey.DeclaringEntityType.Builder);
            }
        }

        /// <summary>
        ///     Called after the base type of an entity type changes.
        /// </summary>
        /// <param name="entityTypeBuilder"> The builder for the entity type. </param>
        /// <param name="newBaseType"> The new base entity type. </param>
        /// <param name="oldBaseType"> The old base entity type. </param>
        /// <param name="context"> Additional information associated with convention execution. </param>
        public virtual void ProcessEntityTypeBaseTypeChanged(
            IConventionEntityTypeBuilder entityTypeBuilder,
            IConventionEntityType newBaseType,
            IConventionEntityType oldBaseType,
            IConventionContext<IConventionEntityType> context)
        {
            if (entityTypeBuilder.Metadata.BaseType != newBaseType)
            {
                return;
            }

            var baseKeys = newBaseType?.GetKeys().ToList();
            var baseIndexes = newBaseType?.GetIndexes().ToList();
            foreach (var foreignKey in entityTypeBuilder.Metadata.GetDeclaredForeignKeys()
                .Concat(entityTypeBuilder.Metadata.GetDerivedForeignKeys()))
            {
                var index = foreignKey.DeclaringEntityType.FindIndex(foreignKey.Properties);
                if (index == null)
                {
                    CreateIndex(foreignKey.Properties, foreignKey.IsUnique, foreignKey.DeclaringEntityType.Builder);
                }
                else if (newBaseType != null)
                {
                    var coveringKey = baseKeys.FirstOrDefault(
                        k => AreIndexedBy(foreignKey.Properties, foreignKey.IsUnique, k.Properties, coveringIndexUniqueness: true));
                    if (coveringKey != null)
                    {
                        RemoveIndex(index);
                    }
                    else
                    {
                        var coveringIndex = baseIndexes.FirstOrDefault(
                            i => AreIndexedBy(foreignKey.Properties, foreignKey.IsUnique, i.Properties, i.IsUnique));
                        if (coveringIndex != null)
                        {
                            RemoveIndex(index);
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Called after an index is added to the entity type.
        /// </summary>
        /// <param name="indexBuilder"> The builder for the index. </param>
        /// <param name="context"> Additional information associated with convention execution. </param>
        public virtual void ProcessIndexAdded(IConventionIndexBuilder indexBuilder, IConventionContext<IConventionIndexBuilder> context)
        {
            var index = indexBuilder.Metadata;
            foreach (var otherIndex in index.DeclaringEntityType.GetDerivedTypesInclusive()
                .SelectMany(t => t.GetDeclaredIndexes())
                .Where(i => i != index && AreIndexedBy(i.Properties, i.IsUnique, index.Properties, index.IsUnique)).ToList())
            {
                RemoveIndex(otherIndex);
            }
        }

        /// <summary>
        ///     Called after an index is removed.
        /// </summary>
        /// <param name="entityTypeBuilder"> The builder for the entity type. </param>
        /// <param name="index"> The removed index. </param>
        /// <param name="context"> Additional information associated with convention execution. </param>
        public virtual void ProcessIndexRemoved(
            IConventionEntityTypeBuilder entityTypeBuilder,
            IConventionIndex index,
            IConventionContext<IConventionIndex> context)
        {
            foreach (var foreignKey in index.DeclaringEntityType.GetDerivedTypesInclusive()
                .SelectMany(t => t.GetDeclaredForeignKeys())
                .Where(fk => AreIndexedBy(fk.Properties, fk.IsUnique, index.Properties, index.IsUnique)))
            {
                CreateIndex(foreignKey.Properties, foreignKey.IsUnique, foreignKey.DeclaringEntityType.Builder);
            }
        }

        /// <summary>
        ///     Called after the uniqueness for a foreign key is changed.
        /// </summary>
        /// <param name="relationshipBuilder"> The builder for the foreign key. </param>
        /// <param name="context"> Additional information associated with convention execution. </param>
        public virtual void ProcessForeignKeyUniquenessChanged(
            IConventionRelationshipBuilder relationshipBuilder, IConventionContext<IConventionRelationshipBuilder> context)
        {
            var foreignKey = relationshipBuilder.Metadata;
            var index = foreignKey.DeclaringEntityType.FindIndex(foreignKey.Properties);
            if (index == null)
            {
                if (foreignKey.IsUnique)
                {
                    CreateIndex(foreignKey.Properties, foreignKey.IsUnique, foreignKey.DeclaringEntityType.Builder);
                }
            }
            else
            {
                if (!foreignKey.IsUnique)
                {
                    var coveringKey = foreignKey.DeclaringEntityType.GetKeys()
                        .FirstOrDefault(k => AreIndexedBy(foreignKey.Properties, false, k.Properties, coveringIndexUniqueness: true));
                    if (coveringKey != null)
                    {
                        RemoveIndex(index);
                        return;
                    }

                    var coveringIndex = foreignKey.DeclaringEntityType.GetIndexes()
                        .FirstOrDefault(i => AreIndexedBy(foreignKey.Properties, false, i.Properties, i.IsUnique));
                    if (coveringIndex != null)
                    {
                        RemoveIndex(index);
                        return;
                    }
                }

                index.Builder.IsUnique(foreignKey.IsUnique);
            }
        }

        /// <summary>
        ///     Called after the uniqueness for an index is changed.
        /// </summary>
        /// <param name="indexBuilder"> The builder for the index. </param>
        /// <param name="context"> Additional information associated with convention execution. </param>
        public virtual void ProcessIndexUniquenessChanged(
            IConventionIndexBuilder indexBuilder, IConventionContext<IConventionIndexBuilder> context)
        {
            var index = indexBuilder.Metadata;
            if (index.IsUnique)
            {
                foreach (var otherIndex in index.DeclaringEntityType.GetDerivedTypesInclusive()
                    .SelectMany(t => t.GetDeclaredIndexes())
                    .Where(i => i != index && AreIndexedBy(i.Properties, i.IsUnique, index.Properties, coveringIndexUniqueness: true))
                    .ToList())
                {
                    RemoveIndex(otherIndex);
                }
            }
            else
            {
                foreach (var foreignKey in index.DeclaringEntityType.GetDerivedTypesInclusive()
                    .SelectMany(t => t.GetDeclaredForeignKeys())
                    .Where(fk => fk.IsUnique && AreIndexedBy(fk.Properties, fk.IsUnique, index.Properties, coveringIndexUniqueness: true)))
                {
                    CreateIndex(foreignKey.Properties, foreignKey.IsUnique, foreignKey.DeclaringEntityType.Builder);
                }
            }
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        protected virtual IConventionIndex CreateIndex(
            [NotNull] IReadOnlyList<IConventionProperty> properties, bool unique, [NotNull] IConventionEntityTypeBuilder entityTypeBuilder)
        {
            foreach (var key in entityTypeBuilder.Metadata.GetKeys())
            {
                if (AreIndexedBy(properties, unique, key.Properties, coveringIndexUniqueness: true))
                {
                    return null;
                }
            }

            foreach (var existingIndex in entityTypeBuilder.Metadata.GetIndexes())
            {
                if (AreIndexedBy(properties, unique, existingIndex.Properties, existingIndex.IsUnique))
                {
                    return null;
                }
            }

            var indexBuilder = entityTypeBuilder.HasIndex(properties);
            if (unique)
            {
                indexBuilder?.IsUnique(true);
            }

            return indexBuilder?.Metadata;
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        protected virtual bool AreIndexedBy(
            [NotNull] IReadOnlyList<IConventionProperty> properties,
            bool unique,
            [NotNull] IReadOnlyList<IConventionProperty> coveringIndexProperties,
            bool coveringIndexUniqueness)
            => (!unique && coveringIndexProperties.Select(p => p.Name).StartsWith(properties.Select(p => p.Name)))
               || (unique && coveringIndexUniqueness && coveringIndexProperties.SequenceEqual(properties));

        private static void RemoveIndex(IConventionIndex index)
            => index.DeclaringEntityType.Builder.HasNoIndex(index);

        /// <summary>
        ///     Called after a model is finalized.
        /// </summary>
        /// <param name="modelBuilder"> The builder for the model. </param>
        /// <param name="context"> Additional information associated with convention execution. </param>
        public virtual void ProcessModelFinalized(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
        {
            var definition = CoreResources.LogRedundantIndexRemoved(Logger);
            if (definition.GetLogBehavior(Logger) == WarningBehavior.Ignore
                && !Logger.DiagnosticSource.IsEnabled(definition.EventId.Name))
            {
                return;
            }

            foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
            {
                foreach (var declaredForeignKey in entityType.GetDeclaredForeignKeys())
                {
                    foreach (var key in entityType.GetKeys())
                    {
                        if (AreIndexedBy(
                            declaredForeignKey.Properties, declaredForeignKey.IsUnique, key.Properties, coveringIndexUniqueness: true))
                        {
                            if (declaredForeignKey.Properties.Count != key.Properties.Count)
                            {
                                Logger.RedundantIndexRemoved(declaredForeignKey.Properties, key.Properties);
                            }
                        }
                    }

                    foreach (var existingIndex in entityType.GetIndexes())
                    {
                        if (AreIndexedBy(
                            declaredForeignKey.Properties, declaredForeignKey.IsUnique, existingIndex.Properties, existingIndex.IsUnique))
                        {
                            if (declaredForeignKey.Properties.Count != existingIndex.Properties.Count)
                            {
                                Logger.RedundantIndexRemoved(declaredForeignKey.Properties, existingIndex.Properties);
                            }
                        }
                    }
                }
            }
        }
    }
}
