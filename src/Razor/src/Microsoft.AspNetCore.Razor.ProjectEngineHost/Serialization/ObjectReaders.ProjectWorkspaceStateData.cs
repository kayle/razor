﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.AspNetCore.Razor.Serialization;

internal static partial class ObjectReaders
{
    private record struct ProjectWorkspaceStateData(TagHelperDescriptor[] TagHelpers, LanguageVersion CSharpLanguageVersion)
    {
        public static readonly PropertyMap<ProjectWorkspaceStateData> PropertyMap = new(
            (nameof(TagHelpers), ReadTagHelpers),
            (nameof(CSharpLanguageVersion), ReadCSharpLanguageVersion));

        private static void ReadTagHelpers(JsonDataReader reader, ref ProjectWorkspaceStateData data)
            => data.TagHelpers = reader.ReadArray(
                static r => r.ReadNonNullObject(
                    static r => ReadTagHelperFromProperties(r, useCache: true)))
                ?? Array.Empty<TagHelperDescriptor>();

        private static void ReadCSharpLanguageVersion(JsonDataReader reader, ref ProjectWorkspaceStateData data)
            => data.CSharpLanguageVersion = (LanguageVersion)reader.ReadInt32();
    }
}
