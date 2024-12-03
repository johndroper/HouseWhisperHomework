using Amazon.BedrockRuntime.Model;
using Amazon.BedrockRuntime;
using Amazon.Runtime;

namespace HouseWhisperHomework
{
  public class AwsHelper
  {
    public IConfiguration Configuration { get; }

    public AwsHelper(IConfiguration configuration)
    {
      Configuration = configuration;
    }
    public async Task<string> GetFromAws(string prompt)
    {
      var awsKey = Configuration["AwsKey"];
      if (String.IsNullOrWhiteSpace(awsKey))
        throw new Exception("AWS Key is empty in appsettings.");
      var awsSecret = Configuration["AwsSecret"];
      if (String.IsNullOrWhiteSpace(awsSecret))
        throw new Exception("AWS Secret is empty in appsettings.");
      var awsRegion = Configuration["AwsRegion"];
      if (String.IsNullOrWhiteSpace(awsRegion))
        throw new Exception("AWS Region is empty in appsettings.");
      var modelId = Configuration["AwsModelId"];
      if (String.IsNullOrWhiteSpace(modelId))
        throw new Exception("AWS ModelId is empty in appsettings.");
      try
      {
        BasicAWSCredentials awsCredentials = new(awsKey, awsSecret);
        var region = Amazon.RegionEndpoint.GetBySystemName(awsRegion);
        AmazonBedrockRuntimeClient client = new AmazonBedrockRuntimeClient(awsCredentials, region);
        var message = new Message();
        message.Content = new List<ContentBlock> { new ContentBlock { Text = prompt } };
        message.Role = ConversationRole.User;
        ConverseRequest request = new ConverseRequest
        {
          ModelId = modelId,
          Messages = new List<Message> { message }
        };
        var converseResponse = await client.ConverseAsync(request);
        return converseResponse.Output.Message.Content.First().Text;
      }
      catch (Exception ex)
      {
        Exception exception = new("Error accessing Bedrock.", ex);
        exception.Data.Add("awsRegion", awsRegion);
        exception.Data.Add("modelId", modelId);
        throw exception;
      }
    }

  }
}
