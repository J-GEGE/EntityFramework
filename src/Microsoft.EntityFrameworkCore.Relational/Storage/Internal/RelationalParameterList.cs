﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Utilities;

namespace Microsoft.EntityFrameworkCore.Storage.Internal
{
    public class RelationalParameterList : IRelationalParameterList
    {
        private readonly List<IRelationalParameter> _parameters = new List<IRelationalParameter>();

        public RelationalParameterList([NotNull] IRelationalTypeMapper typeMapper)
        {
            Check.NotNull(typeMapper, nameof(typeMapper));

            TypeMapper = typeMapper;
        }

        public virtual IReadOnlyList<IRelationalParameter> Parameters => _parameters;

        protected virtual IRelationalTypeMapper TypeMapper { get; }

        public virtual void AddParameter(
            [NotNull] string invariantName,
            [NotNull] string name)
            => _parameters.Add(
                new DynamicRelationalParameter(
                    Check.NotEmpty(invariantName, nameof(invariantName)),
                    Check.NotEmpty(name, nameof(name)),
                    TypeMapper));

        public virtual void AddParameter(
            [NotNull] string invariantName,
            [NotNull] string name,
            [NotNull] Type type)
        {
            Check.NotEmpty(invariantName, nameof(invariantName));
            Check.NotEmpty(name, nameof(name));
            Check.NotNull(type, nameof(type));

            _parameters.Add(
                new TypeMappedRelationalParameter(
                    invariantName,
                    name,
                    TypeMapper.GetMapping(type),
                    type.IsNullableType()));
        }

        public virtual void AddParameter(
            [NotNull] string invariantName,
            [NotNull] string name,
            [NotNull] IProperty property)
        {
            Check.NotEmpty(invariantName, nameof(invariantName));
            Check.NotEmpty(name, nameof(name));
            Check.NotNull(property, nameof(property));

            _parameters.Add(
                new TypeMappedRelationalParameter(
                    invariantName,
                    name,
                    TypeMapper.GetMapping(property),
                    property.IsNullable));
        }

        public virtual void AddCompositeParameter(
            [NotNull] string invariantName,
            [NotNull] Action<IRelationalParameterList> listAction)
        {
            Check.NotEmpty(invariantName, nameof(invariantName));
            Check.NotNull(listAction, nameof(listAction));

            var innerList = new RelationalParameterList(TypeMapper);

            listAction(innerList);

            if (innerList.Parameters.Count > 0)
            {
                _parameters.Add(
                    new CompositeRelationalParameter(
                        invariantName,
                        innerList.Parameters));
            }
        }
    }
}
