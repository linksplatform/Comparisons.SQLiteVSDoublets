using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Comparisons.SQLiteVSDoublets.Model
{
    public static class BlogPosts
    {
        public static IReadOnlyList<BlogPost> List { get; private set; }

        static BlogPosts()
        {
            List = new List<BlogPost>();
        }

        static public void GenerateData(int numberOfRecords)
        {
            var contentStrings = new string[]
            {
                "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Duis malesuada blandit mauris nec bibendum. Phasellus feugiat vehicula mauris et aliquet. Integer et gravida velit, in rutrum leo. Duis pretium, nunc ac posuere porttitor, augue sapien commodo tortor, nec consequat lorem eros ultricies odio. Aliquam varius congue ex nec viverra. Pellentesque eu velit tellus. Donec ac luctus nisi. Curabitur dignissim sodales mauris eu semper. Ut pretium lorem nulla, sit amet auctor arcu placerat vitae. Quisque lacinia dolor et consectetur fermentum. Nam ac orci vitae nulla aliquam tempor ac a nibh. Ut ac tincidunt lacus. Morbi vitae felis lorem.",
                "Curabitur tincidunt nibh sit amet finibus dictum. Suspendisse aliquet arcu non rutrum ultrices. Integer ullamcorper mauris sit amet nibh aliquam, et tempor turpis hendrerit. In molestie elit et mauris rutrum, non auctor ligula ultricies. Vestibulum dignissim mauris finibus libero interdum hendrerit. Nunc vitae ipsum porttitor, egestas magna ut, sagittis sem. Donec euismod ac tortor vel porta. Vivamus convallis, ex at vestibulum rutrum, velit purus venenatis metus, sit amet aliquam sapien nibh quis elit. Aenean id neque a orci sodales venenatis. Integer ut orci ligula. Interdum et malesuada fames ac ante ipsum primis in faucibus. Praesent molestie dolor non lobortis ornare. Duis quis nisl sollicitudin, accumsan ante sed, eleifend velit. Maecenas maximus sed ante nec auctor.",
                "Donec vitae felis lectus. Aenean velit sapien, porttitor ut feugiat a, consectetur et risus. Proin ac viverra sem. Nullam sagittis ex tortor, eu pellentesque tellus efficitur at. Nunc non egestas leo. Nam sed suscipit neque. Nam sodales vel neque eget eleifend. Vivamus in condimentum elit, consectetur commodo ex. Suspendisse rutrum, sapien efficitur cursus sodales, dolor orci pulvinar mauris, eu fringilla leo ex id leo. Interdum et malesuada fames ac ante ipsum primis in faucibus. Proin rhoncus sapien massa, molestie vestibulum augue hendrerit nec. Aliquam malesuada varius sapien id accumsan. Duis blandit aliquet felis, nec pellentesque lacus tincidunt et. Cras sed ligula vel nisl laoreet sagittis. Vestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia Curae; Praesent tristique a neque aliquet aliquam.",
                "Aliquam sed egestas felis. Maecenas sollicitudin nisl in sapien posuere vulputate. Suspendisse eleifend sem magna, interdum consectetur augue venenatis at. Vivamus ornare orci vel orci sodales maximus. Donec ultricies felis ac nulla fermentum gravida. Phasellus vulputate turpis odio, a varius nibh luctus et. Aliquam tincidunt, metus ut congue porttitor, nibh dui ullamcorper quam, a eleifend elit ipsum sit amet quam. Aenean venenatis mollis interdum. Nunc cursus ex sit amet enim lacinia hendrerit. Nullam at libero iaculis, consectetur velit in, porta sem. Ut mattis ut ex in imperdiet. Maecenas pellentesque sit amet dui eget vehicula. Sed posuere, arcu pretium convallis tincidunt, turpis leo dignissim felis, non euismod diam magna a risus. Suspendisse a arcu nec turpis pulvinar ullamcorper. Nunc iaculis malesuada elit eu pretium. Aenean a neque a sapien tincidunt faucibus.",
                "Ut a eleifend augue, eget posuere augue. Proin purus neque, pretium condimentum ipsum ut, venenatis tincidunt nunc. In vitae odio in justo pharetra tincidunt. Maecenas vel tellus interdum, suscipit tellus sit amet, cursus justo. Mauris sollicitudin euismod molestie. Cras eros nisi, molestie vel elementum ut, consequat ac nunc. In consectetur nulla vitae interdum elementum. Praesent faucibus magna et iaculis congue. Curabitur convallis cursus porttitor. Praesent hendrerit justo ut sem convallis sollicitudin eu at odio."
            };
            var blogPosts = new BlogPost[numberOfRecords];
            Random random = new Random();
            for (int i = 0; i < numberOfRecords; i++)
            {
                blogPosts[i] = new BlogPost()
                {
                    Title = $"Blog post {i + 1}",
                    Content = contentStrings[random.Next(0, 5)],
                    PublicationDateTime = DateTime.UtcNow.AddDays(random.NextDouble() * -30)
                };
            }
            List = new ReadOnlyCollection<BlogPost>(blogPosts);
        }
    }
}
