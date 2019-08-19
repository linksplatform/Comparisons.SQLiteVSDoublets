using System;
using System.Collections.Generic;
using System.Linq;
using Platform.Disposables;
using Platform.Collections.Arrays;
using Platform.Data;
using Platform.Data.Doublets;
using Platform.Data.Doublets.UnaryNumbers;
using Platform.Data.Doublets.Decorators;
using Platform.Data.Doublets.Incrementers;
using Platform.Data.Doublets.ResizableDirectMemory;
using Platform.Data.Doublets.Sequences.Converters;
using Platform.Data.Doublets.PropertyOperators;
using Platform.Data.Doublets.Sequences.Indexes;
using Platform.Data.Doublets.Sequences.Walkers;
using Platform.Data.Doublets.Unicode;
using Comparisons.SQLiteVSDoublets.Model;
using LinkAddress = System.UInt32;

namespace Comparisons.SQLiteVSDoublets.Doublets
{
    public class DoubletsDbContext : DisposableBase
    {
        private readonly LinkAddress _meaningRoot;
        private readonly LinkAddress _unaryOne;
        private readonly LinkAddress _unicodeSymbolMarker;
        private readonly LinkAddress _unicodeSequenceMarker;
        private readonly LinkAddress _frequencyMarker;
        private readonly LinkAddress _frequencyPropertyMarker;
        private readonly LinkAddress _titlePropertyMarker;
        private readonly LinkAddress _contentPropertyMarker;
        private readonly LinkAddress _publicationDateTimePropertyMarker;
        private readonly LinkAddress _blogPostMarker;
        private readonly PropertiesOperator<LinkAddress> _defaultLinkPropertyOperator;
        private readonly StringToUnicodeSequenceConverter<LinkAddress> _stringToUnicodeSymbolConverter;
        private readonly UnicodeSequenceToStringConverter<LinkAddress> _unicodeSequenceToStringConverter;
        private readonly ILinks<LinkAddress> _disposableLinks;
        private readonly ILinks<LinkAddress> _links;

        public DoubletsDbContext(string dbFilename)
        {
            _disposableLinks = new ResizableDirectMemoryLinks<LinkAddress>(dbFilename);
            _links = _disposableLinks;
            _links = new LinksCascadeUsagesResolver<LinkAddress>(_links);
            _links = new NonNullContentsLinkDeletionResolver<LinkAddress>(_links);
            _links = new LinksCascadeUniquenessAndUsagesResolver<LinkAddress>(_links);
            _links = new LinksItselfConstantToSelfReferenceResolver<LinkAddress>(_links);
            _links = new LinksInnerReferenceExistenceValidator<LinkAddress>(_links);

            LinkAddress currentMappingLinkIndex = 1;
            _meaningRoot = GerOrCreateMeaningRoot(currentMappingLinkIndex++);
            _unaryOne = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _unicodeSymbolMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _unicodeSequenceMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _frequencyMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _frequencyPropertyMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _titlePropertyMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _contentPropertyMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _publicationDateTimePropertyMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _blogPostMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);

            _defaultLinkPropertyOperator = new PropertiesOperator<LinkAddress>(_links);

            // Create StringToUnicodeSequenceConverter and UnicodeSequenceToStringConverter
            var unaryNumberToAddressConverter = new UnaryNumberToAddressAddOperationConverter<LinkAddress>(_links, _unaryOne);
            var powerOf2ToUnaryNumberConverter = new PowerOf2ToUnaryNumberConverter<LinkAddress>(_links, _unaryOne);
            var addressToUnaryNumberConverter = new AddressToUnaryNumberConverter<LinkAddress>(_links, powerOf2ToUnaryNumberConverter);
            var unaryNumberIncrementer = new UnaryNumberIncrementer<LinkAddress>(_links, _unaryOne);
            var frequencyIncrementer = new FrequencyIncrementer<LinkAddress>(_links, _frequencyMarker, _unaryOne, unaryNumberIncrementer);
            var frequencyPropertyOperator = new PropertyOperator<LinkAddress>(_links, _frequencyPropertyMarker, _frequencyMarker);
            var index = new FrequencyIncrementingSequenceIndex<LinkAddress>(_links, frequencyPropertyOperator, frequencyIncrementer);
            var linkToItsFrequencyNumberConverter = new LinkToItsFrequencyNumberConveter<LinkAddress>(_links, frequencyPropertyOperator, unaryNumberToAddressConverter);
            var sequenceToItsLocalElementLevelsConverter = new SequenceToItsLocalElementLevelsConverter<LinkAddress>(_links, linkToItsFrequencyNumberConverter);
            var optimalVariantConverter = new OptimalVariantConverter<LinkAddress>(_links, sequenceToItsLocalElementLevelsConverter);
            var unicodeSymbolCriterionMatcher = new UnicodeSymbolCriterionMatcher<LinkAddress>(_links, _unicodeSymbolMarker);
            var unicodeSequenceCriterionMatcher = new UnicodeSequenceCriterionMatcher<LinkAddress>(_links, _unicodeSequenceMarker);
            var charToUnicodeSymbolConverter = new CharToUnicodeSymbolConverter<LinkAddress>(_links, addressToUnaryNumberConverter, _unicodeSymbolMarker);
            var unicodeSymbolToCharConverter = new UnicodeSymbolToCharConverter<LinkAddress>(_links, unaryNumberToAddressConverter, unicodeSymbolCriterionMatcher);
            var sequenceWalker = new LeveledSequenceWalker<LinkAddress>(_links, unicodeSymbolCriterionMatcher.IsMatched);
            _stringToUnicodeSymbolConverter = new StringToUnicodeSequenceConverter<LinkAddress>(_links, charToUnicodeSymbolConverter, index, optimalVariantConverter, _unicodeSequenceMarker);
            _unicodeSequenceToStringConverter = new UnicodeSequenceToStringConverter<LinkAddress>(_links, unicodeSequenceCriterionMatcher, sequenceWalker, unicodeSymbolToCharConverter);
        }

        private LinkAddress GerOrCreateMeaningRoot(LinkAddress meaningRootIndex) => _links.Exists(meaningRootIndex) ? meaningRootIndex : _links.CreatePoint();

        private LinkAddress GetOrCreateNextMapping(LinkAddress currentMappingIndex) => _links.Exists(currentMappingIndex) ? currentMappingIndex : _links.CreateAndUpdate(_meaningRoot, _links.Constants.Itself);

        public string GetString(LinkAddress sequence) => _unicodeSequenceToStringConverter.Convert(sequence);

        public LinkAddress CreateString(string @string) => _stringToUnicodeSymbolConverter.Convert(@string);

        public IList<BlogPost> BlogPosts => GetBlogPosts();

        public IList<BlogPost> GetBlogPosts()
        {
            var blogPostsCount = _links.Count(_links.Constants.Any, _blogPostMarker, _links.Constants.Any);
            var array = new IList<LinkAddress>[blogPostsCount];
            if (blogPostsCount > 0)
            {
                var arrayFiller = new ArrayFiller<IList<LinkAddress>, LinkAddress>(array, _links.Constants.Continue);
                _links.Each(arrayFiller.AddAndReturnConstant, _links.Constants.Any, _blogPostMarker, _links.Constants.Any);
            }
            return array.Select(GetBlogPost).ToArray();
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

            var publicationDateTimeSequence = _defaultLinkPropertyOperator.GetValue(postLink, _publicationDateTimePropertyMarker);
            var publicationDateTimeString = GetString(publicationDateTimeSequence);
            blogPost.PublicationDateTime = DateTime.ParseExact(publicationDateTimeString, "s", System.Globalization.CultureInfo.InvariantCulture);

            return blogPost;
        }

        public void Delete(LinkAddress link) => _links.Delete(link);

        public LinkAddress CreateBlogPost(BlogPost post)
        {
            var newPostLink = _links.CreateAndUpdate(_blogPostMarker, _links.Constants.Itself);

            _defaultLinkPropertyOperator.SetValue(newPostLink, _titlePropertyMarker, CreateString(post.Title));

            _defaultLinkPropertyOperator.SetValue(newPostLink, _contentPropertyMarker, CreateString(post.Content));

            var publicationDateTimeString = post.PublicationDateTime.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
            var publicationDateTimeSequence = CreateString(publicationDateTimeString);
            _defaultLinkPropertyOperator.SetValue(newPostLink, _publicationDateTimePropertyMarker, publicationDateTimeSequence);

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
