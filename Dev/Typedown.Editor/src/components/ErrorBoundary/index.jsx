import React from "react";
import { remote } from "services/remote";
import { errorText } from "services/errorText";

class ErrorBoundary extends React.Component {

    constructor(props) {
        super(props);
        this.state = { hasError: false };
    }

    static getDerivedStateFromError() {
        return { hasError: true };
    }

    componentDidCatch(error, errorInfo) {
        // Readable whatever was thrown (an object is "[object Object]" as a string), with where React was.
        remote.unhandledException(errorText(error) + '\n' + (error?.stack ?? '') + '\n\n' + JSON.stringify(errorInfo));
    }

    render() {
        if (this.state.hasError) {
            return <></>;
        }
        return this.props.children;
    }
}

export default ErrorBoundary