using System.Text;

namespace Comparisons.SQLiteVSDoublets;

/// <summary>Deterministic data shared by every storage and by the Rust implementation.</summary>
public static class Dataset
{
    private const ulong GoldenGamma = 0x9E37_79B9_7F4A_7C15;

    public static ulong SplitMix64(ulong x)
    {
        var z = x + GoldenGamma;
        z = (z ^ (z >> 30)) * 0xBF58_476D_1CE4_E5B9;
        z = (z ^ (z >> 27)) * 0x94D0_49BB_1331_11EB;
        return z ^ (z >> 31);
    }

    /// <summary>
    /// The <c>(From, To)</c> pair of the link with id <paramref name="i"/>: both reference already created ids
    /// and <c>From + To == i + 1</c>, so every pair is unique.
    /// </summary>
    public static (ulong From, ulong To) Link(ulong i)
    {
        var from = 1 + SplitMix64(2 * i) % i;
        return (from, i + 1 - from);
    }

    /// <summary>
    /// Order-sensitive mix of two numbers; summing mixes detects records that are lost,
    /// duplicated or associated with the wrong id.
    /// </summary>
    public static ulong Mix(ulong a, ulong b) => ((a * GoldenGamma) ^ b) * GoldenGamma;

    public static ulong LinkChecksum(ulong id, ulong from, ulong to) => Mix(Mix(id, from), to);

    public static ulong LinksChecksum(ulong n, bool swapped)
    {
        ulong checksum = 0;
        for (ulong i = 1; i <= n; i++)
        {
            var (from, to) = Link(i);
            checksum += swapped ? LinkChecksum(i, to, from) : LinkChecksum(i, from, to);
        }
        return checksum;
    }

    /// <summary>
    /// Visits <c>1..=n</c> once each in a scattered order, so point operations do not hit
    /// neighbouring records one after another.
    /// </summary>
    public static IEnumerable<ulong> Scattered(ulong n)
    {
        var stride = GoldenGamma % Math.Max(n, 1);
        while (Gcd(stride, n) != 1)
        {
            stride++;
        }
        for (ulong k = 0; k < n; k++)
        {
            yield return 1 + k * stride % n;
        }
    }

    private static ulong Gcd(ulong a, ulong b) => b == 0 ? a : Gcd(b, a % b);

    private const ulong September2020 = 1_600_000_000;
    private const ulong ThirtyDays = 30 * 24 * 60 * 60;

    private static readonly string[] Paragraphs =
    [
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Duis malesuada blandit mauris nec bibendum. Phasellus feugiat vehicula mauris et aliquet. Integer et gravida velit, in rutrum leo. Duis pretium, nunc ac posuere porttitor, augue sapien commodo tortor, nec consequat lorem eros ultricies odio. Aliquam varius congue ex nec viverra. Pellentesque eu velit tellus. Donec ac luctus nisi. Curabitur dignissim sodales mauris eu semper. Ut pretium lorem nulla, sit amet auctor arcu placerat vitae. Quisque lacinia dolor et consectetur fermentum. Nam ac orci vitae nulla aliquam tempor ac a nibh. Ut ac tincidunt lacus. Morbi vitae felis lorem.",
        "Curabitur tincidunt nibh sit amet finibus dictum. Suspendisse aliquet arcu non rutrum ultrices. Integer ullamcorper mauris sit amet nibh aliquam, et tempor turpis hendrerit. In molestie elit et mauris rutrum, non auctor ligula ultricies. Vestibulum dignissim mauris finibus libero interdum hendrerit. Nunc vitae ipsum porttitor, egestas magna ut, sagittis sem. Donec euismod ac tortor vel porta. Vivamus convallis, ex at vestibulum rutrum, velit purus venenatis metus, sit amet aliquam sapien nibh quis elit. Aenean id neque a orci sodales venenatis. Integer ut orci ligula. Interdum et malesuada fames ac ante ipsum primis in faucibus. Praesent molestie dolor non lobortis ornare. Duis quis nisl sollicitudin, accumsan ante sed, eleifend velit. Maecenas maximus sed ante nec auctor.",
        "Donec vitae felis lectus. Aenean velit sapien, porttitor ut feugiat a, consectetur et risus. Proin ac viverra sem. Nullam sagittis ex tortor, eu pellentesque tellus efficitur at. Nunc non egestas leo. Nam sed suscipit neque. Nam sodales vel neque eget eleifend. Vivamus in condimentum elit, consectetur commodo ex. Suspendisse rutrum, sapien efficitur cursus sodales, dolor orci pulvinar mauris, eu fringilla leo ex id leo. Interdum et malesuada fames ac ante ipsum primis in faucibus. Proin rhoncus sapien massa, molestie vestibulum augue hendrerit nec. Aliquam malesuada varius sapien id accumsan. Duis blandit aliquet felis, nec pellentesque lacus tincidunt et. Cras sed ligula vel nisl laoreet sagittis. Vestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia Curae; Praesent tristique a neque aliquet aliquam.",
        "Aliquam sed egestas felis. Maecenas sollicitudin nisl in sapien posuere vulputate. Suspendisse eleifend sem magna, interdum consectetur augue venenatis at. Vivamus ornare orci vel orci sodales maximus. Donec ultricies felis ac nulla fermentum gravida. Phasellus vulputate turpis odio, a varius nibh luctus et. Aliquam tincidunt, metus ut congue porttitor, nibh dui ullamcorper quam, a eleifend elit ipsum sit amet quam. Aenean venenatis mollis interdum. Nunc cursus ex sit amet enim lacinia hendrerit. Nullam at libero iaculis, consectetur velit in, porta sem. Ut mattis ut ex in imperdiet. Maecenas pellentesque sit amet dui eget vehicula. Sed posuere, arcu pretium convallis tincidunt, turpis leo dignissim felis, non euismod diam magna a risus. Suspendisse a arcu nec turpis pulvinar ullamcorper. Nunc iaculis malesuada elit eu pretium. Aenean a neque a sapien tincidunt faucibus.",
        "Ut a eleifend augue, eget posuere augue. Proin purus neque, pretium condimentum ipsum ut, venenatis tincidunt nunc. In vitae odio in justo pharetra tincidunt. Maecenas vel tellus interdum, suscipit tellus sit amet, cursus justo. Mauris sollicitudin euismod molestie. Cras eros nisi, molestie vel elementum ut, consequat ac nunc. In consectetur nulla vitae interdum elementum. Praesent faucibus magna et iaculis congue. Curabitur convallis cursus porttitor. Praesent hendrerit justo ut sem convallis sollicitudin eu at odio.",
    ];

    public static BlogPost BlogPost(ulong i) =>
        new($"Blog post {i}", Paragraphs[i % 5], September2020 + SplitMix64(i) % ThirtyDays);

    public static ulong BlogPostsChecksum(ulong n)
    {
        ulong checksum = 0;
        for (ulong i = 1; i <= n; i++)
        {
            checksum += BlogPost(i).Checksum;
        }
        return checksum;
    }

    /// <summary>
    /// Like <see cref="BlogPostsChecksum"/>, but also checks that the <c>i</c>-th created id returns the <c>i</c>-th post.
    /// </summary>
    public static ulong BlogPostsByNumberChecksum(ulong n)
    {
        ulong checksum = 0;
        for (ulong i = 1; i <= n; i++)
        {
            checksum += Mix(i, BlogPost(i).Checksum);
        }
        return checksum;
    }
}

/// <param name="PublicationDate">Seconds since the Unix epoch.</param>
public sealed record BlogPost(string Title, string Content, ulong PublicationDate)
{
    public ulong Checksum => Bytes(Title) + Bytes(Content) + PublicationDate;

    private static ulong Bytes(string text)
    {
        ulong sum = 0;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            sum += b;
        }
        return sum;
    }
}
