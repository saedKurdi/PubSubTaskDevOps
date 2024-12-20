using Microsoft.AspNetCore.Mvc;
using PubSubRedisTask.Models;
using StackExchange.Redis;
using System.Diagnostics;
using System.Text.Json;

namespace PubSubRedisTask.Controllers;
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private static ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("redis-15253.c277.us-east-1-3.ec2.redns.redis-cloud.com:15253,password=PRgipOSCveFVseB7vo4GX043N1A2tTCa");

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    public IActionResult CreateChannelOnRedis([FromForm] string channelName)
    {
        if (string.IsNullOrWhiteSpace(channelName))
        {
            return BadRequest("Channel name cannot be empty.");
        }

        // Connect to Redis
        var db = redis.GetDatabase();
        var sub =  redis.GetSubscriber();
       
        // Example: Create a list to store as a value
        var messages = new List<string>();

        // Serialize the list to JSON
        string serializedList = JsonSerializer.Serialize(messages);

        // Save the serialized list in a Redis hash
        db.HashSet("Channels", channelName, serializedList);

        TempData["SuccessMessage"] = "Channel created successfully.";

        RedisChannel channel = new RedisChannel(channelName,RedisChannel.PatternMode.Auto);

        sub.Publish(channel,"Channel created from MVC App .");

        return RedirectToAction("Index");
    }

    public IActionResult Index()
    {
        var model = new IndexViewModel
        {
            Channels = GetChannelsFromRedis()
        };
        ViewBag.SuccessMessage = TempData["SuccessMessage"];
        return View(model);
    }


    private List<Dictionary<string,List<string>>> GetChannelsFromRedis()
    {
        var db = redis.GetDatabase();
        var channels = db.HashGetAll("Channels");
        var dictList = new List<Dictionary<string,List<string>>> ();
        foreach (var channel in channels)
        {
            var channelName = channel.Name.ToString();
            var messages = JsonSerializer.Deserialize<List<string>>(channel.Value);
            var channelDict = new Dictionary<string, List<string>>
            {
                {channelName, messages},
            };
            dictList.Add(channelDict);
        }
        return dictList;
    }

    [HttpGet]
    public List<string> GetSelectedChannelMessages(string channelName)
    {
        var db = redis.GetDatabase();
        var selectedChannelMessages = db.HashGet("Channels", channelName).ToString();
        var messages = JsonSerializer.Deserialize<List<string>>(selectedChannelMessages);
        return messages;
    }

    [HttpPost]
    public IActionResult SendMessage(string channelName, string message)
    {
        if (string.IsNullOrWhiteSpace(channelName) || string.IsNullOrWhiteSpace(message))
        {
            return BadRequest("Invalid channel or message.");
        }

        var db = redis.GetDatabase();
        var subscriber = redis.GetSubscriber();

        // Get the current list of messages from the channel
        var selectedChannelMessages = db.HashGet("Channels", channelName).ToString();
        var messages = JsonSerializer.Deserialize<List<string>>(selectedChannelMessages) ?? new List<string>();

        // Add the new message to the list
        messages.Add(message);

        // Serialize the updated message list
        string updatedMessages = JsonSerializer.Serialize(messages);

        // Update the Redis hash with the new list of messages
        db.HashSet("Channels", channelName, updatedMessages);

        // Publish the message to the Redis channel
        subscriber.Publish(channelName, message);

        return Ok();
    }


    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
