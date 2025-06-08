/*




        ,ad8888ba,         db         88         db
       d8"'    `"8b       d88b        88        d88b
      d8'                d8'`8b       88       d8'`8b
      88                d8'  `8b      88      d8'  `8b
      88      88888    d8YaaaaY8b     88     d8YaaaaY8b
      Y8,        88   d8""""""""8b    88    d8""""""""8b
       Y8a.    .a88  d8'        `8b   88   d8'        `8b
        `"Y88888P"  d8'          `8b  88  d8'          `8b

                        WEAVER OF WORLDS

*/


namespace Gaia.Common.Utils.Caching;

public interface ICacheCapability<GaiaResource>
{
    void CacheResource(GaiaResource resource);
    GaiaResource RetrieveResourceFromCache(string resourceHash);

    /// <summary>
    /// Can be used to retrieve a resource from cache if the provided resource argument contains enough
    /// information in order to determine the identity of the full resource with all its fields.
    /// </summary>
    /// <param name="partialResource"> The resource with parts of its fields filled out which contains the necessary information
    /// to uniquely identify the entire resource</param>
    /// <returns></returns>
    GaiaResource RetrieveResourceFromCache(GaiaResource partialResource);

    bool ResourceExists(string resourceHash);
    bool ResourceExists(GaiaResource partialResource);
}