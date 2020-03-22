using System;
using System.Collections.Generic;
using System.Linq;
using Platform.Disposables;
using Platform.Collections.Lists;
using Platform.Collections.Stacks;
using Platform.Converters;
using Platform.Data;
using Platform.Data.Numbers.Raw;
using Platform.Data.Doublets;
using Platform.Data.Doublets.Decorators;
using Platform.Data.Doublets.PropertyOperators;
using Platform.Data.Doublets.Unicode;
using Platform.Data.Doublets.Sequences;
using Platform.Data.Doublets.Sequences.Walkers;
using Platform.Data.Doublets.Sequences.Converters;
using Comparisons.SQLiteVSDoublets.Model;
using TLinkAddress = System.UInt32;
using Platform.Data.Doublets.CriterionMatchers;
using Platform.Data.Doublets.Memory.Split.Specific;
using System.IO;
using Platform.Memory;
using System.Runtime.CompilerServices;
using Platform.Numbers;
using Platform.Reflection;

namespace Comparisons.SQLiteVSDoublets.Doublets
{
    public class NumberToLongRawNumberSequenceConverter<TSource, TTarget> : LinksDecoratorBase<TTarget>, IConverter<TSource, TTarget>
    {
        private static readonly Comparer<TSource> _comparer = Comparer<TSource>.Default;
        private static readonly TSource _maximumValue = NumericType<TSource>.MaxValue;
        private static readonly int _bitsPerRawNumber = NumericType<TTarget>.BitsSize - 1;
        private static readonly TSource _bitMask = Bit.ShiftRight(_maximumValue, NumericType<TTarget>.BitsSize + 1);
        private static readonly TSource _maximumConvertableAddress = CheckedConverter<TTarget, TSource>.Default.Convert(Arithmetic.Decrement(Hybrid<TTarget>.ExternalZero));
        private static readonly UncheckedConverter<TSource, TTarget> _sourceToTargetConverter = UncheckedConverter<TSource, TTarget>.Default;

        private readonly IConverter<TTarget> _addressToNumberConverter;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NumberToLongRawNumberSequenceConverter(ILinks<TTarget> links, IConverter<TTarget> addressToNumberConverter) : base(links) => _addressToNumberConverter = addressToNumberConverter;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TTarget Convert(TSource source)
        {
            if (_comparer.Compare(source, _maximumConvertableAddress) > 0)
            {
                var numberPart = Bit.And(source, _bitMask);
                var convertedNumber = _addressToNumberConverter.Convert(_sourceToTargetConverter.Convert(numberPart));
                return Links.GetOrCreate(convertedNumber, Convert(Bit.ShiftRight(source, _bitsPerRawNumber)));
            }
            else
            {
                return _addressToNumberConverter.Convert(_sourceToTargetConverter.Convert(source));
            }
        }
    }

    public class LongRawNumberSequenceToNumberConverter<TSource, TTarget> : LinksDecoratorBase<TSource>, IConverter<TSource, TTarget>
    {
        private static readonly int _bitsPerRawNumber = NumericType<TSource>.BitsSize - 1;
        private static readonly UncheckedConverter<TSource, TTarget> _sourceToTargetConverter = UncheckedConverter<TSource, TTarget>.Default;

        private readonly IConverter<TSource> _numberToAddressConverter;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LongRawNumberSequenceToNumberConverter(ILinks<TSource> links, IConverter<TSource> numberToAddressConverter) : base(links) => _numberToAddressConverter = numberToAddressConverter;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TTarget Convert(TSource source)
        {
            var constants = Links.Constants;
            var externalReferencesRange = constants.ExternalReferencesRange;
            if (externalReferencesRange.HasValue && externalReferencesRange.Value.Contains(source))
            {
                return _sourceToTargetConverter.Convert(_numberToAddressConverter.Convert(source));
            }
            else
            {
                var pair = Links.GetLink(source);
                var walker = new LeftSequenceWalker<TSource>(Links, new DefaultStack<TSource>(), (link) => externalReferencesRange.HasValue && externalReferencesRange.Value.Contains(link));
                TTarget result = default;
                foreach (var element in walker.Walk(source))
                {
                    result = Bit.Or(Bit.ShiftLeft(result, _bitsPerRawNumber), Convert(element));
                }
                return result;
            }
        }
    }

    public class DateTimeToLongRawNumberSequenceConverter<TLink> : IConverter<DateTime, TLink>
    {
        private readonly IConverter<long, TLink> _int64ToLongRawNumberConverter;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DateTimeToLongRawNumberSequenceConverter(IConverter<long, TLink> int64ToLongRawNumberConverter) => _int64ToLongRawNumberConverter = int64ToLongRawNumberConverter;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TLink Convert(DateTime source) => _int64ToLongRawNumberConverter.Convert(source.ToFileTimeUtc());
    }

    public class LongRawNumberSequenceToDateTimeConverter<TLink> : IConverter<TLink, DateTime>
    {
        private readonly IConverter<TLink, long> _longRawNumberConverterToInt64;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LongRawNumberSequenceToDateTimeConverter(IConverter<TLink, long> longRawNumberConverterToInt64) => _longRawNumberConverterToInt64 = longRawNumberConverterToInt64;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DateTime Convert(TLink source) => DateTime.FromFileTimeUtc(_longRawNumberConverterToInt64.Convert(source));
    }

    public class DoubletsDbContext : DisposableBase
    {
        private readonly TLinkAddress _meaningRoot;
        private readonly TLinkAddress _unicodeSymbolMarker;
        private readonly TLinkAddress _unicodeSequenceMarker;
        private readonly TLinkAddress _titlePropertyMarker;
        private readonly TLinkAddress _contentPropertyMarker;
        private readonly TLinkAddress _publicationDateTimePropertyMarker;
        private readonly TLinkAddress _blogPostMarker;
        private readonly PropertiesOperator<TLinkAddress> _defaultLinkPropertyOperator;
        private readonly RawNumberToAddressConverter<TLinkAddress> _numberToAddressConverter;
        private readonly AddressToRawNumberConverter<TLinkAddress> _addressToNumberConverter;
        private readonly LongRawNumberSequenceToDateTimeConverter<TLinkAddress> _longRawNumberToDateTimeConverter;
        private readonly DateTimeToLongRawNumberSequenceConverter<TLinkAddress> _dateTimeToLongRawNumberConverter;
        private readonly IConverter<string, TLinkAddress> _stringToUnicodeSequenceConverter;
        private readonly IConverter<TLinkAddress, string> _unicodeSequenceToStringConverter;
        private readonly ILinks<TLinkAddress> _disposableLinks;
        private readonly ILinks<TLinkAddress> _links;

        public DoubletsDbContext(string dbFilename)
        {
            var dataMemory = new FileMappedResizableDirectMemory(dbFilename);
            var indexMemory = new FileMappedResizableDirectMemory($"{Path.GetFileNameWithoutExtension(dbFilename)}.links.index");

            var linksConstants = new LinksConstants<TLinkAddress>(enableExternalReferencesSupport: true);

            // Init the links storage
            _disposableLinks = new UInt32SplitMemoryLinks(dataMemory, indexMemory, UInt32SplitMemoryLinks.DefaultLinksSizeStep, linksConstants); // Low-level logic
            _links = new UInt32Links(_disposableLinks); // Main logic in the combined decorator

            // Set up constant links (markers, aka mapped links)
            TLinkAddress currentMappingLinkIndex = 1;
            _meaningRoot = GerOrCreateMeaningRoot(currentMappingLinkIndex++);
            _unicodeSymbolMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _unicodeSequenceMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _titlePropertyMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _contentPropertyMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _publicationDateTimePropertyMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _blogPostMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);

            // Create properties operator that is able to control reading and writing properties for any link (object)
            _defaultLinkPropertyOperator = new PropertiesOperator<TLinkAddress>(_links);

            // Create converters that are able to convert link's address (UInt64 value) to a raw number represented with another UInt64 value and back
            _numberToAddressConverter = new RawNumberToAddressConverter<TLinkAddress>();
            _addressToNumberConverter = new AddressToRawNumberConverter<TLinkAddress>();

            // Create converters for dates
            _longRawNumberToDateTimeConverter = new LongRawNumberSequenceToDateTimeConverter<TLinkAddress>(new LongRawNumberSequenceToNumberConverter<TLinkAddress, long>(_links, _numberToAddressConverter));
            _dateTimeToLongRawNumberConverter = new DateTimeToLongRawNumberSequenceConverter<TLinkAddress>(new NumberToLongRawNumberSequenceConverter<long, TLinkAddress>(_links, _addressToNumberConverter));

            // Create converters that are able to convert string to unicode sequence stored as link and back
            var balancedVariantConverter = new BalancedVariantConverter<TLinkAddress>(_links);
            var unicodeSymbolCriterionMatcher = new TargetMatcher<TLinkAddress>(_links, _unicodeSymbolMarker);
            var unicodeSequenceCriterionMatcher = new TargetMatcher<TLinkAddress>(_links, _unicodeSequenceMarker);
            var charToUnicodeSymbolConverter = new CharToUnicodeSymbolConverter<TLinkAddress>(_links, _addressToNumberConverter, _unicodeSymbolMarker);
            var unicodeSymbolToCharConverter = new UnicodeSymbolToCharConverter<TLinkAddress>(_links, _numberToAddressConverter, unicodeSymbolCriterionMatcher);
            var sequenceWalker = new RightSequenceWalker<TLinkAddress>(_links, new DefaultStack<TLinkAddress>(), unicodeSymbolCriterionMatcher.IsMatched);
            _stringToUnicodeSequenceConverter = new CachingConverterDecorator<string, TLinkAddress>(new StringToUnicodeSequenceConverter<TLinkAddress>(_links, charToUnicodeSymbolConverter, balancedVariantConverter, _unicodeSequenceMarker));
            _unicodeSequenceToStringConverter = new CachingConverterDecorator<TLinkAddress, string>(new UnicodeSequenceToStringConverter<TLinkAddress>(_links, unicodeSequenceCriterionMatcher, sequenceWalker, unicodeSymbolToCharConverter));
        }

        private TLinkAddress GerOrCreateMeaningRoot(TLinkAddress meaningRootIndex) => _links.Exists(meaningRootIndex) ? meaningRootIndex : _links.CreatePoint();

        private TLinkAddress GetOrCreateNextMapping(TLinkAddress currentMappingIndex) => _links.Exists(currentMappingIndex) ? currentMappingIndex : _links.CreateAndUpdate(_meaningRoot, _links.Constants.Itself);

        public string ConvertToString(TLinkAddress sequence) => _unicodeSequenceToStringConverter.Convert(sequence);

        public TLinkAddress ConvertToSequence(string @string) => _stringToUnicodeSequenceConverter.Convert(@string);

        public IList<BlogPost> BlogPosts => GetBlogPosts();

        public IList<BlogPost> GetBlogPosts()
        {
            var list = new List<IList<TLinkAddress>>();
            var listFiller = new ListFiller<IList<TLinkAddress>, TLinkAddress>(list, _links.Constants.Continue);
            // Load all links that match the query: (any: _blogPostMarker any) it means, link with any address, _blogPostMarker as source and any link as target.
            // All links that match this query are BlogPosts.
            var any = _links.Constants.Any;
            var query = new Link<TLinkAddress>(any, _blogPostMarker, any);
            _links.Each(listFiller.AddAndReturnConstant, query);
            return list.Select(LoadBlogPost).ToList();
        }

        public BlogPost LoadBlogPost(IList<TLinkAddress> postLink) => LoadBlogPost(postLink[_links.Constants.IndexPart]);

        public BlogPost LoadBlogPost(TLinkAddress postLink)
        {
            var blogPost = new BlogPost();

            blogPost.Id = (int)postLink;

            // Load Title property value from the links storage
            var titleSequence = _defaultLinkPropertyOperator.GetValue(postLink, _titlePropertyMarker);
            blogPost.Title = ConvertToString(titleSequence);

            // Load Content property value from the links storage
            var contentSequence = _defaultLinkPropertyOperator.GetValue(postLink, _contentPropertyMarker);
            blogPost.Content = ConvertToString(contentSequence);

            // Load PublicationDateTime property value from the links storage
            blogPost.PublicationDateTime = _longRawNumberToDateTimeConverter.Convert(_defaultLinkPropertyOperator.GetValue(postLink, _publicationDateTimePropertyMarker));

            return blogPost;
        }

        public TLinkAddress SaveBlogPost(BlogPost post)
        {
            var newPostLink = _links.CreateAndUpdate(_blogPostMarker, _links.Constants.Itself);

            // Save Title property value to the links storage
            _defaultLinkPropertyOperator.SetValue(newPostLink, _titlePropertyMarker, ConvertToSequence(post.Title));

            // Save Content property value to the links storage
            _defaultLinkPropertyOperator.SetValue(newPostLink, _contentPropertyMarker, ConvertToSequence(post.Content));

            // Save PublicationDateTime property value to the links storage
            _defaultLinkPropertyOperator.SetValue(newPostLink, _publicationDateTimePropertyMarker, _dateTimeToLongRawNumberConverter.Convert(post.PublicationDateTime));

            return newPostLink;
        }

        public void Delete(TLinkAddress link) => _links.Delete(link);

        protected override void Dispose(bool manual, bool wasDisposed)
        {
            if (!wasDisposed)
            {
                _disposableLinks.DisposeIfPossible();
            }
        }
    }
}
