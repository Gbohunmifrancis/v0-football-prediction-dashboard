using FootballPrediction.Core.Entities;
using FootballPrediction.Infrastructure.Data;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace FootballPrediction.Infrastructure.Services;

public class InjuryAndNewsScrapingService
{
    private readonly FplDbContext _context;
    private readonly ILogger<InjuryAndNewsScrapingService> _logger;
    private readonly HttpClient _httpClient;

    public InjuryAndNewsScrapingService(FplDbContext context, ILogger<InjuryAndNewsScrapingService> logger, HttpClient httpClient)
    {
        _context = context;
        _logger = logger;
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task ScrapeInjuryUpdatesAsync()
    {
        try
        {
            _logger.LogInformation("Starting injury updates scraping...");
            
            // Scrape from multiple sources
            await ScrapePremierLeagueInjuriesAsync();
            await ScrapePhysioRoomInjuriesAsync();
            await ScrapeBBCSportInjuriesAsync();
            
            _logger.LogInformation("Completed injury updates scraping successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while scraping injury updates");
            throw;
        }
    }

    private async Task ScrapePremierLeagueInjuriesAsync()
    {
        try
        {
            var url = "https://www.premierleague.com/news";
            var html = await _httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var newsItems = doc.DocumentNode.SelectNodes("//article[contains(@class, 'news')]");
            if (newsItems == null) return;

            foreach (var item in newsItems)
            {
                var titleNode = item.SelectSingleNode(".//h3//a");
                var dateNode = item.SelectSingleNode(".//time");
                
                if (titleNode?.InnerText?.ToLower().Contains("injury") == true ||
                    titleNode?.InnerText?.ToLower().Contains("injured") == true ||
                    titleNode?.InnerText?.ToLower().Contains("fitness") == true)
                {
                    await ProcessInjuryNewsAsync(
                        titleNode.InnerText.Trim(),
                        titleNode.GetAttributeValue("href", ""),
                        dateNode?.InnerText?.Trim(),
                        "Premier League Official"
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping Premier League injuries");
        }
    }

    private async Task ScrapePhysioRoomInjuriesAsync()
    {
        try
        {
            var url = "https://www.physioroom.com/news/english_premier_league";
            var html = await _httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var injuryRows = doc.DocumentNode.SelectNodes("//tr[contains(@class, 'injury')]");
            if (injuryRows == null) return;

            foreach (var row in injuryRows)
            {
                var playerCell = row.SelectSingleNode(".//td[1]");
                var injuryCell = row.SelectSingleNode(".//td[2]");
                var statusCell = row.SelectSingleNode(".//td[3]");
                var returnCell = row.SelectSingleNode(".//td[4]");

                if (playerCell != null && injuryCell != null)
                {
                    var playerName = playerCell.InnerText.Trim();
                    var injuryType = injuryCell.InnerText.Trim();
                    var status = statusCell?.InnerText.Trim() ?? "";
                    var returnDate = returnCell?.InnerText.Trim() ?? "";

                    await ProcessPhysioRoomInjuryAsync(playerName, injuryType, status, returnDate);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping PhysioRoom injuries");
        }
    }

    private async Task ScrapeBBCSportInjuriesAsync()
    {
        try
        {
            var url = "https://www.bbc.co.uk/sport/football/premier-league";
            var html = await _httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var newsItems = doc.DocumentNode.SelectNodes("//article[contains(@class, 'gs-c-promo')]");
            if (newsItems == null) return;

            foreach (var item in newsItems)
            {
                var titleNode = item.SelectSingleNode(".//h3");
                var linkNode = item.SelectSingleNode(".//a");
                
                if (titleNode?.InnerText?.ToLower().Contains("injury") == true ||
                    titleNode?.InnerText?.ToLower().Contains("injured") == true)
                {
                    await ProcessInjuryNewsAsync(
                        titleNode.InnerText.Trim(),
                        linkNode?.GetAttributeValue("href", "") ?? "",
                        DateTime.Now.ToString(),
                        "BBC Sport"
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping BBC Sport injuries");
        }
    }

    public async Task ScrapeTransferNewsAsync()
    {
        try
        {
            _logger.LogInformation("Starting transfer news scraping...");
            
            await ScrapeSkySportsTransfersAsync();
            await ScrapeTransfermarktNewsAsync();
            await ScrapeBBCTransferNewsAsync();
            
            _logger.LogInformation("Completed transfer news scraping successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while scraping transfer news");
            throw;
        }
    }

    private async Task ScrapeSkySportsTransfersAsync()
    {
        try
        {
            var url = "https://www.skysports.com/transfer-centre";
            var html = await _httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var transferItems = doc.DocumentNode.SelectNodes("//div[contains(@class, 'news-list__item')]");
            if (transferItems == null) return;

            foreach (var item in transferItems)
            {
                var titleNode = item.SelectSingleNode(".//span[contains(@class, 'news-list__headline')]");
                var timeNode = item.SelectSingleNode(".//span[contains(@class, 'label__timestamp')]");
                
                if (titleNode != null)
                {
                    await ProcessTransferNewsAsync(
                        titleNode.InnerText.Trim(),
                        timeNode?.InnerText?.Trim() ?? DateTime.Now.ToString(),
                        "Sky Sports",
                        "High"
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping Sky Sports transfers");
        }
    }

    private async Task ScrapeTransfermarktNewsAsync()
    {
        try
        {
            var url = "https://www.transfermarkt.com/premier-league/transfers/wettbewerb/GB1";
            var html = await _httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var transferRows = doc.DocumentNode.SelectNodes("//table[contains(@class, 'items')]//tr");
            if (transferRows == null) return;

            foreach (var row in transferRows.Skip(1)) // Skip header
            {
                var playerCell = row.SelectSingleNode(".//td[1]//a");
                var fromTeamCell = row.SelectSingleNode(".//td[3]//img");
                var toTeamCell = row.SelectSingleNode(".//td[5]//img");
                var feeCell = row.SelectSingleNode(".//td[6]");

                if (playerCell != null)
                {
                    var playerName = playerCell.InnerText.Trim();
                    var fromTeam = fromTeamCell?.GetAttributeValue("title", "") ?? "";
                    var toTeam = toTeamCell?.GetAttributeValue("title", "") ?? "";
                    var fee = feeCell?.InnerText.Trim() ?? "";

                    var title = $"{playerName} moves from {fromTeam} to {toTeam}";
                    await ProcessTransferNewsAsync(title, DateTime.Now.ToString(), "Transfermarkt", "Medium");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping Transfermarkt news");
        }
    }

    private async Task ScrapeBBCTransferNewsAsync()
    {
        try
        {
            var url = "https://www.bbc.co.uk/sport/football/transfer-news";
            var html = await _httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var newsItems = doc.DocumentNode.SelectNodes("//article[contains(@class, 'gs-c-promo')]");
            if (newsItems == null) return;

            foreach (var item in newsItems)
            {
                var titleNode = item.SelectSingleNode(".//h3");
                
                if (titleNode != null && ContainsTransferKeywords(titleNode.InnerText))
                {
                    await ProcessTransferNewsAsync(
                        titleNode.InnerText.Trim(),
                        DateTime.Now.ToString(),
                        "BBC Sport",
                        "High"
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping BBC transfer news");
        }
    }

    // Helper methods
    private async Task ProcessInjuryNewsAsync(string title, string url, string? dateStr, string source)
    {
        try
        {
            var playerNames = ExtractPlayerNamesFromTitle(title);
            foreach (var playerName in playerNames)
            {
                var player = await FindPlayerByNameAsync(playerName);
                if (player != null)
                {
                    var injuryType = ExtractInjuryTypeFromTitle(title);
                    var severity = DetermineInjurySeverity(title);
                    
                    var existingInjury = await _context.InjuryUpdates
                        .FirstOrDefaultAsync(i => i.PlayerId == player.Id && 
                                           i.Description.Contains(title.Substring(0, Math.Min(50, title.Length))) &&
                                           i.ReportedDate.Date == DateTime.Now.Date);
                    
                    if (existingInjury == null)
                    {
                        var injuryUpdate = new InjuryUpdate
                        {
                            PlayerId = player.Id,
                            InjuryType = injuryType,
                            Severity = severity,
                            Description = title,
                            ReportedDate = ParseDateString(dateStr) ?? DateTime.Now,
                            Status = "Active",
                            Source = source,
                            LastUpdated = DateTime.UtcNow
                        };
                        
                        _context.InjuryUpdates.Add(injuryUpdate);
                    }
                }
            }
            
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing injury news: {Title}", title);
        }
    }

    private async Task ProcessPhysioRoomInjuryAsync(string playerName, string injuryType, string status, string returnDate)
    {
        try
        {
            var player = await FindPlayerByNameAsync(playerName);
            if (player != null)
            {
                var existingInjury = await _context.InjuryUpdates
                    .Where(i => i.PlayerId == player.Id && i.Status == "Active")
                    .OrderByDescending(i => i.ReportedDate)
                    .FirstOrDefaultAsync();
                
                if (existingInjury == null)
                {
                    var injuryUpdate = new InjuryUpdate
                    {
                        PlayerId = player.Id,
                        InjuryType = injuryType,
                        Severity = DetermineInjurySeverity(injuryType + " " + status),
                        Description = $"{playerName} - {injuryType}",
                        ReportedDate = DateTime.Now,
                        ExpectedReturnDate = ParseReturnDate(returnDate),
                        Status = "Active",
                        Source = "PhysioRoom",
                        LastUpdated = DateTime.UtcNow
                    };
                    
                    _context.InjuryUpdates.Add(injuryUpdate);
                }
                else
                {
                    existingInjury.ExpectedReturnDate = ParseReturnDate(returnDate);
                    existingInjury.LastUpdated = DateTime.UtcNow;
                }
                
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PhysioRoom injury for player: {PlayerName}", playerName);
        }
    }

    private async Task ProcessTransferNewsAsync(string title, string dateStr, string source, string reliability)
    {
        try
        {
            var playerNames = ExtractPlayerNamesFromTitle(title);
            foreach (var playerName in playerNames)
            {
                var player = await FindPlayerByNameAsync(playerName);
                if (player != null)
                {
                    var newsType = DetermineTransferType(title);
                    
                    var existingNews = await _context.TransferNews
                        .FirstOrDefaultAsync(t => t.PlayerId == player.Id && 
                                           t.Title.Contains(title.Substring(0, Math.Min(30, title.Length))) &&
                                           t.PublishedDate.Date == DateTime.Now.Date);
                    
                    if (existingNews == null)
                    {
                        var transferNews = new TransferNews
                        {
                            PlayerId = player.Id,
                            NewsType = newsType,
                            Title = title,
                            Description = title,
                            PublishedDate = ParseDateString(dateStr) ?? DateTime.Now,
                            Source = source,
                            Reliability = reliability,
                            IsConfirmed = source == "Sky Sports" || source == "BBC Sport",
                            LastUpdated = DateTime.UtcNow
                        };
                        
                        _context.TransferNews.Add(transferNews);
                    }
                }
            }
            
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing transfer news: {Title}", title);
        }
    }

    private async Task<Player?> FindPlayerByNameAsync(string playerName)
    {
        var normalizedName = NormalizePlayerName(playerName);
        
        return await _context.Players
            .FirstOrDefaultAsync(p => 
                p.WebName.ToLower().Contains(normalizedName) ||
                p.FirstName.ToLower().Contains(normalizedName) ||
                p.SecondName.ToLower().Contains(normalizedName) ||
                (p.FirstName + " " + p.SecondName).ToLower().Contains(normalizedName));
    }

    private static string NormalizePlayerName(string name)
    {
        return Regex.Replace(name.ToLower().Trim(), @"[^\w\s]", "");
    }

    private static List<string> ExtractPlayerNamesFromTitle(string title)
    {
        var names = new List<string>();
        var words = title.Split(' ');
        
        // Simple heuristic to find player names (capitalized words)
        for (int i = 0; i < words.Length - 1; i++)
        {
            if (char.IsUpper(words[i][0]) && char.IsUpper(words[i + 1][0]) && 
                !IsCommonWord(words[i]) && !IsCommonWord(words[i + 1]))
            {
                names.Add($"{words[i]} {words[i + 1]}");
            }
        }
        
        return names.Distinct().ToList();
    }

    private static bool IsCommonWord(string word)
    {
        var commonWords = new[] { "The", "And", "Or", "But", "In", "On", "At", "To", "For", "Of", "With", "By", "From", "Up", "About", "Into", "Through", "During", "Before", "After", "Above", "Below", "Between", "Among", "Under", "Over" };
        return commonWords.Contains(word);
    }

    private static string ExtractInjuryTypeFromTitle(string title)
    {
        var lowerTitle = title.ToLower();
        
        if (lowerTitle.Contains("knee")) return "Knee";
        if (lowerTitle.Contains("ankle")) return "Ankle";
        if (lowerTitle.Contains("hamstring")) return "Hamstring";
        if (lowerTitle.Contains("muscle")) return "Muscle";
        if (lowerTitle.Contains("back")) return "Back";
        if (lowerTitle.Contains("shoulder")) return "Shoulder";
        if (lowerTitle.Contains("head")) return "Head";
        if (lowerTitle.Contains("concussion")) return "Concussion";
        if (lowerTitle.Contains("groin")) return "Groin";
        if (lowerTitle.Contains("thigh")) return "Thigh";
        
        return "Unknown";
    }

    private static string DetermineInjurySeverity(string text)
    {
        var lowerText = text.ToLower();
        
        if (lowerText.Contains("long") || lowerText.Contains("major") || lowerText.Contains("serious") || lowerText.Contains("months"))
            return "Major";
        if (lowerText.Contains("minor") || lowerText.Contains("slight") || lowerText.Contains("days"))
            return "Minor";
            
        return "Medium";
    }

    private static string DetermineTransferType(string title)
    {
        var lowerTitle = title.ToLower();
        
        if (lowerTitle.Contains("sign") || lowerTitle.Contains("join")) return "Transfer_In";
        if (lowerTitle.Contains("leave") || lowerTitle.Contains("depart")) return "Transfer_Out";
        if (lowerTitle.Contains("loan")) return "Loan";
        if (lowerTitle.Contains("contract") || lowerTitle.Contains("extend")) return "Contract_Extension";
        
        return "Transfer_In";
    }

    private static bool ContainsTransferKeywords(string text)
    {
        var keywords = new[] { "transfer", "sign", "join", "move", "deal", "contract", "loan", "bid", "offer" };
        var lowerText = text.ToLower();
        return keywords.Any(keyword => lowerText.Contains(keyword));
    }

    private static DateTime? ParseDateString(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return null;
        return DateTime.TryParse(dateStr, out var result) ? result : null;
    }

    private static DateTime? ParseReturnDate(string returnDateStr)
    {
        if (string.IsNullOrEmpty(returnDateStr) || returnDateStr.ToLower().Contains("unknown"))
            return null;
            
        if (returnDateStr.ToLower().Contains("week"))
        {
            var match = Regex.Match(returnDateStr, @"(\d+)\s*week");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var weeks))
                return DateTime.Now.AddDays(weeks * 7);
        }
        
        if (returnDateStr.ToLower().Contains("month"))
        {
            var match = Regex.Match(returnDateStr, @"(\d+)\s*month");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var months))
                return DateTime.Now.AddMonths(months);
        }
        
        return DateTime.TryParse(returnDateStr, out var result) ? result : null;
    }
}
