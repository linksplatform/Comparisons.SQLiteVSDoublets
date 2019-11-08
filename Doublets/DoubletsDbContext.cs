using System;
using System.Collections.Generic;
using System.Linq;
using Platform.Disposables;
using Platform.Collections.Arrays;
using Platform.Collections.Lists;
using Platform.Collections.Stacks;
using Platform.Converters;
using Platform.Data;
using Platform.Data.Numbers.Raw;
using Platform.Data.Doublets;
using Platform.Data.Doublets.Decorators;
using Platform.Data.Doublets.ResizableDirectMemory.Specific;
using Platform.Data.Doublets.PropertyOperators;
using Platform.Data.Doublets.Unicode;
using Platform.Data.Doublets.Sequences;
using Platform.Data.Doublets.Sequences.Indexes;
using Platform.Data.Doublets.Sequences.Walkers;
using Platform.Data.Doublets.Sequences.Converters;
using Platform.Data.Doublets.Sequences.Frequencies.Cache;
using Platform.Data.Doublets.Sequences.Frequencies.Counters;
using Comparisons.SQLiteVSDoublets.Model;
using LinkAddress = System.UInt64;

namespace Comparisons.SQLiteVSDoublets.Doublets
{
    public class DoubletsDbContext : DisposableBase
    {
        private readonly LinkAddress _meaningRoot;
        private readonly LinkAddress _unicodeSymbolMarker;
        private readonly LinkAddress _unicodeSequenceMarker;
        private readonly LinkAddress _titlePropertyMarker;
        private readonly LinkAddress _contentPropertyMarker;
        private readonly LinkAddress _publicationDateTimePropertyMarker;
        private readonly LinkAddress _blogPostMarker;
        private readonly PropertiesOperator<LinkAddress> _defaultLinkPropertyOperator;
        private readonly RawNumberToAddressConverter<ulong> _numberToAddressConverter;
        private readonly AddressToRawNumberConverter<ulong> _addressToNumberConverter;
        private readonly IConverter<string, LinkAddress> _stringToUnicodeSequenceConverter;
        private readonly IConverter<LinkAddress, string> _unicodeSequenceToStringConverter;
        private readonly ILinks<LinkAddress> _disposableLinks;
        private readonly ILinks<LinkAddress> _links;

        public DoubletsDbContext(string dbFilename)
        {
            //_disposableLinks = new ResizableDirectMemoryLinks<LinkAddress>(dbFilename);
            //_links = _disposableLinks.DecorateWithAutomaticUniquenessAndUsagesResolution();
            //_links = new LinksItselfConstantToSelfReferenceResolver<LinkAddress>(_links);
            //_links = new LinksInnerReferenceExistenceValidator<LinkAddress>(_links);
            _disposableLinks = new UInt64ResizableDirectMemoryLinks(dbFilename);
            _links = new UInt64Links(_disposableLinks);

            LinkAddress currentMappingLinkIndex = 1;
            _meaningRoot = GerOrCreateMeaningRoot(currentMappingLinkIndex++);
            _unicodeSymbolMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _unicodeSequenceMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _titlePropertyMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _contentPropertyMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _publicationDateTimePropertyMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _blogPostMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);

            _defaultLinkPropertyOperator = new PropertiesOperator<LinkAddress>(_links);

            // Create StringToUnicodeSequenceConverter and UnicodeSequenceToStringConverter
            _numberToAddressConverter = new RawNumberToAddressConverter<LinkAddress>();
            _addressToNumberConverter = new AddressToRawNumberConverter<LinkAddress>();
            //var totalSequenceSymbolFrequencyCounter = new TotalSequenceSymbolFrequencyCounter<LinkAddress>(_links);
            //var linkFrequenciesCache = new LinkFrequenciesCache<LinkAddress>(_links, totalSequenceSymbolFrequencyCounter);
            //var index = new CachedFrequencyIncrementingSequenceIndex<LinkAddress>(linkFrequenciesCache);
            //var linkToItsFrequencyNumberConverter = new FrequenciesCacheBasedLinkToItsFrequencyNumberConverter<LinkAddress>(linkFrequenciesCache);
            //var sequenceToItsLocalElementLevelsConverter = new SequenceToItsLocalElementLevelsConverter<LinkAddress>(_links, linkToItsFrequencyNumberConverter);
            //var optimalVariantConverter = new OptimalVariantConverter<LinkAddress>(_links, sequenceToItsLocalElementLevelsConverter);
            var balancedVariantConverter = new BalancedVariantConverter<LinkAddress>(_links);
            var unicodeSymbolCriterionMatcher = new UnicodeSymbolCriterionMatcher<LinkAddress>(_links, _unicodeSymbolMarker);
            var unicodeSequenceCriterionMatcher = new UnicodeSequenceCriterionMatcher<LinkAddress>(_links, _unicodeSequenceMarker);
            var charToUnicodeSymbolConverter = new CharToUnicodeSymbolConverter<LinkAddress>(_links, _addressToNumberConverter, _unicodeSymbolMarker);
            var unicodeSymbolToCharConverter = new UnicodeSymbolToCharConverter<LinkAddress>(_links, _numberToAddressConverter, unicodeSymbolCriterionMatcher);
            var sequenceWalker = new RightSequenceWalker<LinkAddress>(_links, new DefaultStack<LinkAddress>(), unicodeSymbolCriterionMatcher.IsMatched);
            _stringToUnicodeSequenceConverter = new CachingConverterDecorator<string, LinkAddress>(new StringToUnicodeSequenceConverter<LinkAddress>(_links, charToUnicodeSymbolConverter, new Unindex<LinkAddress>(), balancedVariantConverter, _unicodeSequenceMarker));
            _unicodeSequenceToStringConverter = new CachingConverterDecorator<LinkAddress, string>(new UnicodeSequenceToStringConverter<LinkAddress>(_links, unicodeSequenceCriterionMatcher, sequenceWalker, unicodeSymbolToCharConverter));
        }

        private LinkAddress GerOrCreateMeaningRoot(LinkAddress meaningRootIndex) => _links.Exists(meaningRootIndex) ? meaningRootIndex : _links.CreatePoint();

        private LinkAddress GetOrCreateNextMapping(LinkAddress currentMappingIndex) => _links.Exists(currentMappingIndex) ? currentMappingIndex : _links.CreateAndUpdate(_meaningRoot, _links.Constants.Itself);

        public string GetString(LinkAddress sequence) => _unicodeSequenceToStringConverter.Convert(sequence);

        public LinkAddress CreateString(string @string) => _stringToUnicodeSequenceConverter.Convert(@string);

        public IList<BlogPost> BlogPosts => GetBlogPosts();

        public IList<BlogPost> GetBlogPosts()
        {
            var list = new List<IList<LinkAddress>>();
            var listFiller = new ListFiller<IList<LinkAddress>, LinkAddress>(list, _links.Constants.Continue);
            _links.Each(listFiller.AddAndReturnConstant, _links.Constants.Any, _blogPostMarker, _links.Constants.Any);
            return list.Select(GetBlogPost).ToList();
        }

        public BlogPost GetBlogPost(IList<LinkAddress> postLink) => GetBlogPost(postLink[_links.Constants.IndexPart]);

        public BlogPost GetBlogPost(LinkAddress postLink)
        {
            var blogPost = new BlogPost();

            blogPost.Id = (int)postLink;

            var titleSequence = _defaultLinkPropertyOperator.GetValue(postLink, _titlePropertyMarker);
            blogPost.Title = GetString(titleSequence);

            var contentSequence = _defaultLinkPropertyOperator.GetValue(postLink, _contentPropertyMarker);
            blogPost.Content = GetString(contentSequence);

            //var publicationDateTimeSequence = _defaultLinkPropertyOperator.GetValue(postLink, _publicationDateTimePropertyMarker);
            //var publicationDateTimeString = GetString(publicationDateTimeSequence);
            //blogPost.PublicationDateTime = DateTime.ParseExact(publicationDateTimeString, "s", System.Globalization.CultureInfo.InvariantCulture);

            blogPost.PublicationDateTime = DateTime.FromFileTimeUtc((long)_numberToAddressConverter.Convert(_defaultLinkPropertyOperator.GetValue(postLink, _publicationDateTimePropertyMarker)));

            return blogPost;
        }

        public void Delete(LinkAddress link) => _links.Delete(link);

        public LinkAddress CreateBlogPost(BlogPost post)
        {
            var newPostLink = _links.CreateAndUpdate(_blogPostMarker, _links.Constants.Itself);

            _defaultLinkPropertyOperator.SetValue(newPostLink, _titlePropertyMarker, CreateString(post.Title));

            _defaultLinkPropertyOperator.SetValue(newPostLink, _contentPropertyMarker, CreateString(post.Content));

            //var publicationDateTimeString = post.PublicationDateTime.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
            //var publicationDateTimeSequence = CreateString(publicationDateTimeString);
            //_defaultLinkPropertyOperator.SetValue(newPostLink, _publicationDateTimePropertyMarker, publicationDateTimeSequence);

            _defaultLinkPropertyOperator.SetValue(newPostLink, _publicationDateTimePropertyMarker, _addressToNumberConverter.Convert((ulong)post.PublicationDateTime.ToFileTimeUtc()));

            return newPostLink;
        }

        protected override void Dispose(bool manual, bool wasDisposed)
        {
            if (!wasDisposed)
            {
                _disposableLinks.DisposeIfPossible();
            }
        }
    }
}
