using Microsoft.EntityFrameworkCore;
using LiteDB;

var builder = WebApplication.CreateBuilder(args);

var connectionString = "short.db";
builder.Services.AddSingleton<ILiteDatabase, LiteDatabase>(
   x => new LiteDatabase(connectionString));


builder.Services.AddDbContext<TodoDb>(opt => opt.UseInMemoryDatabase("TodoList"));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
var app = builder.Build();

// app.MapGet("/", ()=> "Raaaamin");
// app.MapGet("/", ()=> new {Message= "Raaaamin"});

app.MapGet("/", async (HttpContext ctx) =>
{
	//sets the content type as html
    ctx.Response.Headers.ContentType = new Microsoft.Extensions.Primitives.StringValues("text/html; charset=UTF-8");
    await ctx.Response.SendFileAsync("wwwroot/index.html");
});

app.MapGet("/{chunck}", (string chunck, ILiteDatabase db) =>
    db.GetCollection<ShortUrl>().FindOne(x => x.Chunck == chunck)
    is ShortUrl url
    ? Results.Redirect(url.Url)
    : Results.NotFound());

app.MapPost("/urls", (ShortUrl shortUrl, HttpContext ctx, ILiteDatabase db) =>
{
	//check if is a valid url
    if (Uri.TryCreate(shortUrl.Url, UriKind.RelativeOrAbsolute
       , out Uri? parsedUri))
    {
        //generates a random value
        shortUrl.Chunck = Nanoid.Nanoid.Generate(size: 9);
        //inserts new record in the database
        db.GetCollection<ShortUrl>(BsonAutoId.Guid).Insert(shortUrl);
        var rawShortUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}/{shortUrl.Chunck}";
        return Results.Ok(new { ShortUrl = rawShortUrl });
    }
    return Results.BadRequest(new { ErrorMessage = "Invalid Url" });
});

RouteGroupBuilder todoItems = app.MapGroup("/todoitems");
todoItems.MapGet("/", GetAllTodos);
todoItems.MapGet("/complete", GetCompleteTodos);
todoItems.MapGet("/{id}", GetTodo);
todoItems.MapPost("/", CreateTodo);
todoItems.MapPut("/{id}", UpdateTodo);
todoItems.MapDelete("/{id}", DeleteTodo);

app.Run();

static async Task<IResult> GetAllTodos(TodoDb db)
{
    return TypedResults.Ok(await db.Todos.Select(x => new TodoItemDTO(x)).ToArrayAsync());
}

static async Task<IResult> GetCompleteTodos(TodoDb db) {
    return TypedResults.Ok(await db.Todos.Where(t => t.IsComplete).Select(x => new TodoItemDTO(x)).ToListAsync());
}

static async Task<IResult> GetTodo(int id, TodoDb db)
{
    return await db.Todos.FindAsync(id)
        is Todo todo
            ? TypedResults.Ok(new TodoItemDTO(todo))
            : TypedResults.NotFound();
}

static async Task<IResult> CreateTodo(TodoItemDTO todoItemDTO, TodoDb db)
{
    var todoItem = new Todo
    {
        IsComplete = todoItemDTO.IsComplete,
        Name = todoItemDTO.Name
    };

    db.Todos.Add(todoItem);
    await db.SaveChangesAsync();

    todoItemDTO = new TodoItemDTO(todoItem);

    return TypedResults.Created($"/todoitems/{todoItem.Id}", todoItemDTO);
}

static async Task<IResult> UpdateTodo(int id, TodoItemDTO todoItemDTO, TodoDb db)
{
    var todo = await db.Todos.FindAsync(id);

    if (todo is null) return TypedResults.NotFound();

    todo.Name = todoItemDTO.Name;
    todo.IsComplete = todoItemDTO.IsComplete;

    await db.SaveChangesAsync();

    return TypedResults.NoContent();
}

static async Task<IResult> DeleteTodo(int id, TodoDb db)
{
    if (await db.Todos.FindAsync(id) is Todo todo)
    {
        db.Todos.Remove(todo);
        await db.SaveChangesAsync();
        return TypedResults.Ok(todo);
    }

    return TypedResults.NotFound();
}
static async Task<IResult> ReturnDefault()
{
    return TypedResults.Ok("Where is Ramin");
}
