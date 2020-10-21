﻿using System.Collections.Generic;
using System.Linq;
using Sprache;

namespace DockerfileModel
{
    public static class ParserExtensions
    {
        public static Parser<IEnumerable<T>> AsEnumerable<T>(this Parser<T> parser) =>
            from item in parser
            select new T[] { item };

        public static Parser<IEnumerable<T>> Flatten<T>(this Parser<IEnumerable<IEnumerable<T>>> parser) =>
            from itemSets in parser
            select itemSets.SelectMany(items => items);

        public static Parser<T> Single<T>(this Parser<IEnumerable<T>> parser) =>
            from items in parser
            select items.Single();

        public static Parser<TResult> Cast<TSource, TResult>(this Parser<TSource> parser) =>
            from item in parser
            select (TResult)(object)item;

        public static Parser<string> ConvertToString<T>(this Parser<T> parser) =>
            from item in parser
            select item.ToString();
    }
}
