using System;                    
using System.Collections.Generic; 
using System.Linq;                

namespace DvSqlGenWeb.Search
{
   
    public sealed class SearchFilter
    {
        
        public Guid? DocKindId { get; set; } 

       
        public string AuthorLastNamePrefix { get; set; } = ""; 


        public string Query { get; set; } = ""; 

       
        public bool CanBeHandledByMetadataFilterOnly() 
        {
            return DocKindId.HasValue && !string.IsNullOrWhiteSpace(AuthorLastNamePrefix); 
        }
    }


    public sealed class SearchResult
    {
        public Guid InstanceId { get; set; }                     
        public string? Title { get; set; }                       
        public string? CardType { get; set; }                   
        public Guid? CardTypeId { get; set; }                    
        public Guid? DocKindId { get; set; }                    
        public string? AuthorFio { get; set; }                   
        public double BestScore { get; set; }                    
        public List<string> BestSnippets { get; set; } = new();  

        
        public override string ToString() => $"{InstanceId} | {Title} | score={BestScore}"; 
    }


    public sealed class ChromaHit
    {
        public string Id { get; set; } = "";                      
        public string Document { get; set; } = "";                
        public Dictionary<string, object?> Metadata { get; set; } 
        public double Distance { get; set; }                      
    }


    public sealed class PagedResults<T>
    {
        public IReadOnlyList<T> Items { get; }      
        public int Total { get; }                  
        public int Page { get; }                    
        public int PageSize { get; }                

        public PagedResults(IReadOnlyList<T> items, int total, int page, int pageSize) 
        {
            Items = items;                          
            Total = total;                          
            Page = page;                            
            PageSize = pageSize;                    
        }
    }
}
