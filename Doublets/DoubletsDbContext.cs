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
using Platform.Data.Doublets.Memory.United.Specific;
using Platform.Data.Doublets.PropertyOperators;
using Platform.Data.Doublets.Unicode;
using Platform.Data.Doublets.Sequences;
using Platform.Data.Doublets.Sequences.Walkers;
using Platform.Data.Doublets.Sequences.Converters;
using Comparisons.SQLiteVSDoublets.Model;
using LinkAddress = System.UInt64;
using Platform.Data.Doublets.CriterionMatchers;

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
            // Init the links storage
            _disposableLinks = new UInt64UnitedMemoryLinks(dbFilename); // Low-level logic
            _links = new UInt64Links(_disposableLinks); // Main logic in the combined decorator

            // Set up constant links (markers, aka mapped links)
            LinkAddress currentMappingLinkIndex = 1;
            _meaningRoot = GerOrCreateMeaningRoot(currentMappingLinkIndex++);
            _unicodeSymbolMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _unicodeSequenceMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _titlePropertyMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _contentPropertyMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _publicationDateTimePropertyMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);
            _blogPostMarker = GetOrCreateNextMapping(currentMappingLinkIndex++);

            // Create properties operator that is able to control reading and writing properties for any link (object)
            _defaultLinkPropertyOperator = new PropertiesOperator<LinkAddress>(_links);

            // Create converters that are able to convert link's address (UInt64 value) to a raw number represented with another UInt64 value and back
            _numberToAddressConverter = new RawNumberToAddressConverter<LinkAddress>();
            _addressToNumberConverter = new AddressToRawNumberConverter<LinkAddress>();

            // Create converters that are able to convert string to unicode sequence stored as link and back
            var balancedVariantConverter = new BalancedVariantConverter<LinkAddress>(_links);
            var unicodeSymbolCriterionMatcher = new TargetMatcher<LinkAddress>(_links, _unicodeSymbolMarker);
            var unicodeSequenceCriterionMatcher = new TargetMatcher<LinkAddress>(_links, _unicodeSequenceMarker);
            var charToUnicodeSymbolConverter = new CharToUnicodeSymbolConverter<LinkAddress>(_links, _addressToNumberConverter, _unicodeSymbolMarker);
            var unicodeSymbolToCharConverter = new UnicodeSymbolToCharConverter<LinkAddress>(_links, _numberToAddressConverter, unicodeSymbolCriterionMatcher);
            var sequenceWalker = new RightSequenceWalker<LinkAddress>(_links, new DefaultStack<LinkAddress>(), unicodeSymbolCriterionMatcher.IsMatched);
            _stringToUnicodeSequenceConverter = new CachingConverterDecorator<string, LinkAddress>(new StringToUnicodeSequenceConverter<LinkAddress>(_links, charToUnicodeSymbolConverter, balancedVariantConverter, _unicodeSequenceMarker));
            _unicodeSequenceToStringConverter = new CachingConverterDecorator<LinkAddress, string>(new UnicodeSequenceToStringConverter<LinkAddress>(_links, unicodeSequenceCriterionMatcher, sequenceWalker, unicodeSymbolToCharConverter));
        }

        private LinkAddress GerOrCreateMeaningRoot(LinkAddress meaningRootIndex) => _links.Exists(meaningRootIndex) ? meaningRootIndex : _links.CreatePoint();

        private LinkAddress GetOrCreateNextMapping(LinkAddress currentMappingIndex) => _links.Exists(currentMappingIndex) ? currentMappingIndex : _links.CreateAndUpdate(_meaningRoot, _links.Constants.Itself);

        public string ConvertToString(LinkAddress sequence) => _unicodeSequenceToStringConverter.Convert(sequence);

        public LinkAddress ConvertToSequence(string @string) => _stringToUnicodeSequenceConverter.Convert(@string);

        public IList<BlogPost> BlogPosts => GetBlogPosts();

        public IList<BlogPost> GetBlogPosts()
        {
            var list = new List<IList<LinkAddress>>();
            var listFiller = new ListFiller<IList<LinkAddress>, LinkAddress>(list, _links.Constants.Continue);
            // Load all links that match the query: (any: _blogPostMarker any) it means, link with any address, _blogPostMarker as source and any link as target.
            // All links that match this query are BlogPosts.
            var any = _links.Constants.Any;
            var query = new Link<LinkAddress>(any, _blogPostMarker, any);
            _links.Each(listFiller.AddAndReturnConstant, query);
            return list.Select(LoadBlogPost).ToList();
        }

        public BlogPost LoadBlogPost(IList<LinkAddress> postLink) => LoadBlogPost(postLink[_links.Constants.IndexPart]);

        public BlogPost LoadBlogPost(LinkAddress postLink)
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
            blogPost.PublicationDateTime = DateTime.FromFileTimeUtc((long)_numberToAddressConverter.Convert(_defaultLinkPropertyOperator.GetValue(postLink, _publicationDateTimePropertyMarker)));

            return blogPost;
        }

        public LinkAddress SaveBlogPost(BlogPost post)
        {
            var newPostLink = _links.CreateAndUpdate(_blogPostMarker, _links.Constants.Itself);

            // Save Title property value to the links storage
            _defaultLinkPropertyOperator.SetValue(newPostLink, _titlePropertyMarker, ConvertToSequence(post.Title));

            // Save Content property value to the links storage
            _defaultLinkPropertyOperator.SetValue(newPostLink, _contentPropertyMarker, ConvertToSequence(post.Content));

            // Save PublicationDateTime property value to the links storage
            _defaultLinkPropertyOperator.SetValue(newPostLink, _publicationDateTimePropertyMarker, _addressToNumberConverter.Convert((ulong)post.PublicationDateTime.ToFileTimeUtc()));

            return newPostLink;
        }

        public void Delete(LinkAddress link) => _links.Delete(link);

        protected override void Dispose(bool manual, bool wasDisposed)
        {
            if (!wasDisposed)
            {
                _disposableLinks.DisposeIfPossible();
            }
        }
    }
}
