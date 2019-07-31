using System;
using System.Collections.Generic;
using System.Linq;
using Platform.Collections.Arrays;
using Platform.Data;
using Platform.Data.Doublets;
using Platform.Data.Doublets.Converters;
using Platform.Data.Doublets.Decorators;
using Platform.Data.Doublets.Incrementers;
using Platform.Data.Doublets.PropertyOperators;
using Platform.Data.Doublets.ResizableDirectMemory;
using Platform.Data.Doublets.Sequences;
using Platform.Data.Doublets.Sequences.Converters;
using Comparisons.SQLiteVSDoublets.Model;

namespace Comparisons.SQLiteVSDoublets.Doublets
{
    public class DoubletsDbContext : UInt64Links
    {
        private readonly Sequences _sequences;
        private readonly ulong _meaningRoot;
        private readonly ulong _unaryOne;
        private readonly ulong _sequenceMarker;
        private readonly ulong _frequencyMarker;
        private readonly ulong _frequencyPropertyMarker;
        private readonly ulong _titlePropertyMarker;
        private readonly ulong _contentPropertyMarker;
        private readonly ulong _publicationDateTimePropertyMarker;
        private readonly ulong _blogPostMarker;
        private readonly DefaultLinkPropertyOperator<ulong> _defaultLinkPropertyOperator;
        private readonly LinkFrequencyIncrementer<ulong> _linkFrequencyIncrementer;

        public DoubletsDbContext(string dbFilename) 
            : base(new UInt64ResizableDirectMemoryLinks(dbFilename))
        {
            this.UseUnicode();

            var currentMappingLinkIndex = UnicodeMap.LastCharLink + 1;
            _meaningRoot = GerOrCreateMeaningRoot(currentMappingLinkIndex);
            _unaryOne = GetOrCreateNextMapping(++currentMappingLinkIndex);
            _sequenceMarker = GetOrCreateNextMapping(++currentMappingLinkIndex);
            _frequencyMarker = GetOrCreateNextMapping(++currentMappingLinkIndex);
            _frequencyPropertyMarker = GetOrCreateNextMapping(++currentMappingLinkIndex);
            _titlePropertyMarker = GetOrCreateNextMapping(++currentMappingLinkIndex);
            _contentPropertyMarker = GetOrCreateNextMapping(++currentMappingLinkIndex);
            _publicationDateTimePropertyMarker = GetOrCreateNextMapping(++currentMappingLinkIndex);
            _blogPostMarker = GetOrCreateNextMapping(++currentMappingLinkIndex);

            _defaultLinkPropertyOperator = new DefaultLinkPropertyOperator<ulong>(this);

            // Create LinkFrequencyIncrementer and OptimalVariantConverter
            var unaryNumberToAddressConveter = new UnaryNumberToAddressAddOperationConverter<ulong>(this, _unaryOne);
            var unaryNumberIncrementer = new UnaryNumberIncrementer<ulong>(this, _unaryOne);
            var frequencyIncrementer = new FrequencyIncrementer<ulong>(this, _frequencyMarker, _unaryOne, unaryNumberIncrementer);
            var frequencyPropertyOperator = new FrequencyPropertyOperator<ulong>(this, _frequencyPropertyMarker, _frequencyMarker);
            _linkFrequencyIncrementer = new LinkFrequencyIncrementer<ulong>(this, frequencyPropertyOperator, frequencyIncrementer); // TODO: Put it directly to Sequences after fix of https://github.com/linksplatform/Data.Doublets/issues/2
            var linkToItsFrequencyNumberConverter = new LinkToItsFrequencyNumberConveter<ulong>(this, frequencyPropertyOperator, unaryNumberToAddressConveter);
            var sequenceToItsLocalElementLevelsConverter = new SequenceToItsLocalElementLevelsConverter<ulong>(this, linkToItsFrequencyNumberConverter);
            var optimalVariantConverter = new OptimalVariantConverter<ulong>(this, sequenceToItsLocalElementLevelsConverter);

            var syncLinks = new SynchronizedLinks<ulong>(this);
            _sequences = new Sequences(syncLinks, new SequencesOptions<ulong>() { UseSequenceMarker = true, SequenceMarkerLink = _sequenceMarker, LinksToSequenceConverter = optimalVariantConverter });
        }

        private ulong GerOrCreateMeaningRoot(ulong meaningRootIndex) => this.Exists(meaningRootIndex) ? meaningRootIndex : this.CreatePoint();

        private ulong GetOrCreateNextMapping(ulong currentMappingIndex) => this.Exists(currentMappingIndex) ? currentMappingIndex : this.CreateAndUpdate(_meaningRoot, Constants.Itself);

        public string GetString(ulong sequence)
        {
            var links = _sequences.ReadSequenceCore(sequence, UnicodeMap.IsCharLink);
            return UnicodeMap.FromLinksToString(links);
        }

        public ulong CreateString(string @string)
        {
            var links = UnicodeMap.FromStringToLinkArray(@string);
            _linkFrequencyIncrementer.Increment(links); // TODO: Remove it after fix of https://github.com/linksplatform/Data.Doublets/issues/2
            var sequence = _sequences.Create(links);
            return sequence;
        }

        public IList<BlogPost> BlogPosts => GetBlogPosts();

        public IList<BlogPost> GetBlogPosts()
        {
            var blogPostsCount = this.Count(Constants.Any, _blogPostMarker, Constants.Any);
            var array = new IList<ulong>[blogPostsCount];
            if (blogPostsCount > 0)
            {
                var arrayFiller = new ArrayFiller<IList<ulong>, ulong>(array, Constants.Continue);
                this.Each(arrayFiller.AddAndReturnConstant, Constants.Any, _blogPostMarker, Constants.Any);
            }
            return array.Select(GetBlogPost).ToArray();
        }

        public BlogPost GetBlogPost(IList<ulong> postLink) => GetBlogPost(postLink[Constants.IndexPart]);

        public BlogPost GetBlogPost(ulong postLink)
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

        public ulong CreateBlogPost(BlogPost post)
        {
            var newPostLink = this.CreateAndUpdate(_blogPostMarker, Constants.Itself);

            _defaultLinkPropertyOperator.SetValue(newPostLink, _titlePropertyMarker, CreateString(post.Title));

            _defaultLinkPropertyOperator.SetValue(newPostLink, _contentPropertyMarker, CreateString(post.Content));

            var publicationDateTimeString = post.PublicationDateTime.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
            var publicationDateTimeSequence = CreateString(publicationDateTimeString);
            _defaultLinkPropertyOperator.SetValue(newPostLink, _publicationDateTimePropertyMarker, publicationDateTimeSequence);

            return newPostLink;
        }
    }
}
