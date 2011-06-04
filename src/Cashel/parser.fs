namespace Cashel

[<AutoOpen>]
module Parser =
              
    type Parser<'input, 'result> = 'input -> ('result * 'input) option
    
    //I define the monadic operators (zero, result, bind & choice) as individual 
    //functions so I can use them outside the parse monad
    let result v : Parser<'i,'r> = fun input -> Some(v, input)
    
    let bind p f : Parser<'i,'r> = 
        fun input ->
            match p input with
            | Some(value, input) -> f value input
            | None -> None
    
    let zero : Parser<'i,'r> = fun input -> None
    
    let choice p q : Parser<'i,'r> = 
        fun input ->
            match p input with
            | Some(v) -> Some(v)
            | None -> q input
    
    //I define infix operator versions of bind and choice to make it more 
    //convienent to call
    let inline (>>=) p f = bind p f
    let inline (+++) p q = choice p q
    
    //Here's the parser monad definition
    type ParserBuilder() =
        member w.Delay(f) = fun input -> f () input 
        member w.Zero() = zero
        member w.Return(v) = result v 
        member w.ReturnFrom(m) = m
        member w.Bind(p, f) = p >>= f
        member w.Combine(p1,p2) = p1 +++ p2
        
    let parser = ParserBuilder()
  
    ///map applies the function f to the results of the parser p
    let map p f = p >>= (fun x -> result (f x))
         
    ///filter checks the value returned from item against the predicate function f
    let filter p f = p >>= (fun x -> if f x then result x else zero)
   
    ///apply
    let inline (<*>) f a = f >>= (fun f' -> a >>= (fun a' -> result (f' a')))

    ///lift
    let inline (<!>) f a = map a f
   