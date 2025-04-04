import { internalPropCache } from "../state-manager";
import { isObservableArray } from "./knockout";
import { logError } from "./logging";
import { keys } from "./objects";

/**
 * Traverses provided context according to given path.
 * @example / - returns context
 * @example "" or null - returns context
 * @example /exampleProp/A - returns prop A located withing property exampleProp located at provided context
 * @example /exampleProp/B/1 - returns second item from collection B located within property exampleProp located at provided context
 * @returns found property as unwrapped object
 */
export function traverseContext(context: any, path: string): any {
    if (path == null)
        path = "";

    var parts = path.split("/");

    var currentLevel = context;
    var currentPath = "";
    for (var i = 0; i < parts.length; i++) {
        let expressionPart = parts[i];
        if (expressionPart === "")
            continue;

        var nextNode = ko.unwrap(currentLevel)[expressionPart];
        if (nextNode == undefined) {
            throw `Validation error could not been applied to property specified by propertyPath ${path}. Property with name ${expressionPart} does not exist on ${!currentPath ? "root" : currentPath}.`;
        }
        currentPath += `/${expressionPart}`;
        currentLevel = nextNode;
    }

    return currentLevel
}

export function findPathToChildObservable(vm: any, child: any, path: string): string | null {
    if (vm == child) {
        // We found the child
        return path;
    }

    vm = ko.unwrap(vm);

    if (typeof vm !== "object" || vm == null) {
        return null;
    }

    // vm is object, we can also try comparing the unwrapped value
    if (vm == ko.unwrap(child)) {
        return path
    }

    if (Array.isArray(vm)) {
        // Iterate over its elements
        let index = 0;
        for (const value of vm) {
            let result = findPathToChildObservable(value, child, path + "/" + index)
            if (result != null)
                return result;
            index++;
        }
    }
    else {
        // Iterate over its properties, if it's FakeObservableObject, only already initialized properties are iterated
        for (const propertyName of keys(vm[internalPropCache] ?? vm)) {
            var propertyValue = vm[propertyName];
            if (propertyName.startsWith('$') || propertyValue == null) {
                continue;
            }

            let result = findPathToChildObservable(propertyValue, child, path + "/" + propertyName);
            if (result != null)
                return result;
        }
    }

    return null;
}

export function getDataSourceItems(viewModel: any): Array<KnockoutObservable<any>> {
    const value = ko.unwrap(viewModel);
    if (value == null) {
        return [];
    }
    return ko.unwrap(value.Items || value);
}

export function wrapObservable(func: () => any, isArray?: boolean): KnockoutComputed<any> {
    let wrapper = ko.pureComputed({
        read: () => ko.unwrap(getExpressionResult(func)),
        write: value => updateObservable(func, value)
    });

    if (isArray) {
        proxyObservableArrayMethods(wrapper, () => getExpressionResult(func))
    }

    return wrapper.extend({ notify: "always" });
}

function updateObservable(getObservable: () => KnockoutObservable<any>, value: any) {
    const result = getExpressionResult(getObservable);

    if (!ko.isWriteableObservable(result)) {
        logError("validation", `Cannot write a value to ko.computed because the expression '${getObservable}' does not return a writable observable.`);
    } else {
        result(value);
    }
}

export function proxyObservableArrayMethods(wrapper: KnockoutObservable<any>, getObservableArray: () => any) {
    for (const fnName of ["push", "pop", "unshift", "shift", "reverse", "sort", "splice", "slice", "replace", "indexOf", "remove", "removeAll"]) {
        (wrapper as any)[fnName] = (...args: any) => {
            const result = getExpressionResult(getObservableArray)
        
            if (!isObservableArray(result)) {
                return logError("validation", compileConstants.debug ? `Cannot execute '${fnName}' function on ko.computed because the expression '${getObservableArray}' does not return an observable array.` : 'Target is not observableArray')
            } else {
                return result[fnName](...args)
            }
        }
    }
    wrapper.extend({ trackArrayChanges: true })
}
export const unwrapComputedProperty = (obs: any) =>
    ko.isComputed(obs) && "wrappedProperty" in obs ?
    (obs as any)["wrappedProperty"]() : // workaround for dotvvm-with-control-properties handler
    obs;

function getExpressionResult(func: () => any) {
    return unwrapComputedProperty(func());
}
