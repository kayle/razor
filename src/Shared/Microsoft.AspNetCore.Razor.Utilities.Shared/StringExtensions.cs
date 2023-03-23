﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace System;

internal static class StringExtensions
{
    public static bool IsNullOrEmpty([NotNullWhen(false)] this string? s)
        => string.IsNullOrEmpty(s);

    public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? s)
        => string.IsNullOrWhiteSpace(s);
}